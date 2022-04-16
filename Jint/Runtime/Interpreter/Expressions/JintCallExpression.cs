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

        public JintCallExpression(CallExpression expression) : base(expression)
        {
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            var engine = context.Engine;
            var expression = (CallExpression) _expression;
            _calleeExpression = Build(engine, expression.Callee);
            var expressions = new JintExpression[expression.Arguments.Count];

            static bool CanSpread(Node e)
            {
                return e?.Type == Nodes.SpreadElement
                    || e is AssignmentExpression ae && ae.Right.Type == Nodes.SpreadElement;
            }

            var hasSpreads = false;
            var cacheable = true;
            for (var i = 0; i < expression.Arguments.Count; i++)
            {
                var expressionArgument = expression.Arguments[i];
                expressions[i] = Build(engine, expressionArgument);
                cacheable &= expressionArgument.Type == Nodes.Literal;
                hasSpreads |= CanSpread(expressionArgument);
                if (expressionArgument is ArrayExpression ae)
                {
                    for (var elementIndex = 0; elementIndex < ae.Elements.Count; elementIndex++)
                    {
                        hasSpreads |= CanSpread(ae.Elements[elementIndex]);
                    }
                }
            }

            var cachedArgumentsHolder = new CachedArgumentsHolder(expressions, hasSpreads);
            if (cacheable)
            {
                _cached = true;
                cachedArgumentsHolder.CacheArguments(context, hasSpreads);
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
                var evalArg = argList.Length > 0 ? argList[0] : null;

                if (argList.Length == 0)
                {
                    return Undefined.Instance;
                }

                var evalFunctionInstance = (EvalFunctionInstance) func;
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
                        thisValue = value;
                    }
                    else
                    {
                        var refEnv = (EnvironmentRecord) baseValue;
                        thisValue = refEnv.WithBaseObject();   
                    }
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

        private Arguments ArgumentListEvaluation(EvaluationContext context)
        {
            var cachedArguments = _cachedArguments;
            Arguments arguments;
            if (_cached)
            {
                arguments = cachedArguments.CachedArguments;
            }
            else
            {
                arguments = cachedArguments.ArgumentBuilder.Build(context);
            }

            return arguments;
        }

        private sealed class CachedArgumentsHolder
        {
            public CachedArgumentsHolder(JintExpression[] expressions, bool hasSpreads)
            {
                ArgumentBuilder = CallArgumentsBuilder.GetArgumentsBuilder(expressions, hasSpreads);
            }

            internal readonly CallArgumentsBuilder ArgumentBuilder;
            internal Arguments CachedArguments;

            public void CacheArguments(EvaluationContext context, bool hasSpreads)
            {
                CachedArguments = ArgumentBuilder.Build(context);
            }
        }
    }
}
