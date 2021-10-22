using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintCallExpression : JintExpression
    {
        private CachedArgumentsHolder _cachedArguments;
        private bool _cached;

        private JintExpression _calleeExpression;
        private bool _hasSpreads;

        public JintCallExpression(CallExpression expression) : base(expression)
        {
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            var engine = context.Engine;
            var expression = (CallExpression) _expression;
            _calleeExpression = Build(engine, expression.Callee);
            var cachedArgumentsHolder = new CachedArgumentsHolder
            {
                JintArguments = new JintExpression[expression.Arguments.Count]
            };

            static bool CanSpread(Node e)
            {
                return e?.Type == Nodes.SpreadElement
                    || e is AssignmentExpression ae && ae.Right?.Type == Nodes.SpreadElement;
            }

            bool cacheable = true;
            for (var i = 0; i < expression.Arguments.Count; i++)
            {
                var expressionArgument = expression.Arguments[i];
                cachedArgumentsHolder.JintArguments[i] = Build(engine, expressionArgument);
                cacheable &= expressionArgument.Type == Nodes.Literal;
                _hasSpreads |= CanSpread(expressionArgument);
                if (expressionArgument is ArrayExpression ae)
                {
                    for (var elementIndex = 0; elementIndex < ae.Elements.Count; elementIndex++)
                    {
                        _hasSpreads |= CanSpread(ae.Elements[elementIndex]);
                    }
                }
            }

            if (cacheable)
            {
                _cached = true;
                var arguments = System.Array.Empty<JsValue>();
                if (cachedArgumentsHolder.JintArguments.Length > 0)
                {
                    arguments = new JsValue[cachedArgumentsHolder.JintArguments.Length];
                    BuildArguments(context, cachedArgumentsHolder.JintArguments, arguments);
                }

                cachedArgumentsHolder.CachedArguments = arguments;
            }

            _cachedArguments = cachedArgumentsHolder;
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            return NormalCompletion(_calleeExpression is JintSuperExpression
                ? SuperCall(context)
                : Call(context)
            );
        }

        private JsValue SuperCall(EvaluationContext context)
        {
            var engine = context.Engine;
            var thisEnvironment = (FunctionEnvironmentRecord) engine.ExecutionContext.GetThisEnvironment();
            var newTarget = engine.GetNewTarget(thisEnvironment);
            var func = GetSuperConstructor(thisEnvironment);
            if (!func.IsConstructor)
            {
                ExceptionHelper.ThrowTypeError(engine.Realm, "Not a constructor");
            }

            var argList = ArgumentListEvaluation(context);
            var result = ((IConstructor) func).Construct(argList, newTarget);
            var thisER = (FunctionEnvironmentRecord) engine.ExecutionContext.GetThisEnvironment();
            return thisER.BindThisValue(result);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getsuperconstructor
        /// </summary>
        private static ObjectInstance GetSuperConstructor(FunctionEnvironmentRecord thisEnvironment)
        {
            var envRec = thisEnvironment;
            var activeFunction = envRec._functionObject;
            var superConstructor = activeFunction.GetPrototypeOf();
            return superConstructor;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-function-calls
        /// </summary>
        private JsValue Call(EvaluationContext context)
        {
            var reference = _calleeExpression.Evaluate(context).Value;

            if (ReferenceEquals(reference, Undefined.Instance))
            {
                return Undefined.Instance;
            }

            var engine = context.Engine;
            var func = engine.GetValue(reference, false);

            if (reference is Reference referenceRecord
                && !referenceRecord.IsPropertyReference()
                && referenceRecord.GetReferencedName() == CommonProperties.Eval
                && ReferenceEquals(func, engine.Realm.Intrinsics.Eval))
            {
                var argList = ArgumentListEvaluation(context);
                if (argList.Length == 0)
                {
                    return Undefined.Instance;
                }

                var evalFunctionInstance = (EvalFunctionInstance) func;
                var evalArg = argList[0];
                var strictCaller = StrictModeScope.IsStrictModeCode;
                var evalRealm = evalFunctionInstance._realm;
                var direct = !((CallExpression) _expression).Optional;
                var value = evalFunctionInstance.PerformEval(evalArg, evalRealm, strictCaller, direct);
                engine._referencePool.Return(referenceRecord);
                return value;
            }

            var thisCall = (CallExpression) _expression;
            var tailCall = IsInTailPosition(thisCall);
            return EvaluateCall(context, func, reference, thisCall.Arguments, tailCall);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-evaluatecall
        /// </summary>
        private JsValue EvaluateCall(EvaluationContext context, JsValue func, object reference, in NodeList<Expression> arguments, bool tailPosition)
        {
            JsValue thisValue;
            var referenceRecord = reference as Reference;
            var engine = context.Engine;
            if (referenceRecord is not null)
            {
                if (referenceRecord.IsPropertyReference())
                {
                    thisValue = referenceRecord.GetThisValue();
                }
                else
                {
                    var baseValue = referenceRecord.GetBase();

                    // deviation from the spec to support null-propagation helper
                    if (baseValue.IsNullOrUndefined()
                        && engine._referenceResolver.TryUnresolvableReference(engine, referenceRecord, out var value))
                    {
                        return value;
                    }

                    var refEnv = (EnvironmentRecord) baseValue;
                    thisValue = refEnv.WithBaseObject();
                }
            }
            else
            {
                thisValue = Undefined.Instance;
            }

            var argList = ArgumentListEvaluation(context);

            if (!func.IsObject() && !engine._referenceResolver.TryGetCallable(engine, reference, out func))
            {
                var message = referenceRecord == null
                    ? reference + " is not a function"
                    : $"Property '{referenceRecord.GetReferencedName()}' of object is not a function";
                ExceptionHelper.ThrowTypeError(engine.Realm, message);
            }

            var callable = func as ICallable;
            if (callable is null)
            {
                var message = $"{referenceRecord?.GetReferencedName() ?? reference} is not a function";
                ExceptionHelper.ThrowTypeError(engine.Realm, message);
            }

            if (tailPosition)
            {
                // TODO tail call
                // PrepareForTailCall();
            }

            var result = engine.Call(callable, thisValue, argList, _calleeExpression);

            if (!_cached && argList.Length > 0)
            {
                engine._jsValueArrayPool.ReturnArray(argList);
            }

            engine._referencePool.Return(referenceRecord);
            return result;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-isintailposition
        /// </summary>
        private static bool IsInTailPosition(CallExpression call)
        {
            // TODO tail calls
            return false;
        }

        private JsValue[] ArgumentListEvaluation(EvaluationContext context)
        {
            var cachedArguments = _cachedArguments;
            var arguments = System.Array.Empty<JsValue>();
            if (_cached)
            {
                arguments = cachedArguments.CachedArguments;
            }
            else
            {
                if (cachedArguments.JintArguments.Length > 0)
                {
                    if (_hasSpreads)
                    {
                        arguments = BuildArgumentsWithSpreads(context, cachedArguments.JintArguments);
                    }
                    else
                    {
                        arguments = context.Engine._jsValueArrayPool.RentArray(cachedArguments.JintArguments.Length);
                        BuildArguments(context, cachedArguments.JintArguments, arguments);
                    }
                }
            }

            return arguments;
        }

        private class CachedArgumentsHolder
        {
            internal JintExpression[] JintArguments;
            internal JsValue[] CachedArguments;
        }
    }
}