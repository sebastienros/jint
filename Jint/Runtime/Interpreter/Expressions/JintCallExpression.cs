using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
        private CachedArgumentsHolder _cachedArguments = null!;
        private bool _cached;

        private JintExpression _calleeExpression = null!;
        private bool _hasSpreads;

        public JintCallExpression(CallExpression expression) : base(expression)
        {
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            var expression = (CallExpression) _expression;
            ref readonly var expressionArguments = ref expression.Arguments;

            _calleeExpression = Build(expression.Callee);
            var cachedArgumentsHolder = new CachedArgumentsHolder
            {
                JintArguments = new JintExpression[expressionArguments.Count]
            };

            static bool CanSpread(Node? e)
            {
                if (e is null)
                {
                    return false;
                }

                return e.Type == Nodes.SpreadElement || e is AssignmentExpression { Right.Type: Nodes.SpreadElement };
            }

            var cacheable = true;
            for (var i = 0; i < expressionArguments.Count; i++)
            {
                var expressionArgument = expressionArguments[i];
                cachedArgumentsHolder.JintArguments[i] = Build(expressionArgument);
                cacheable &= expressionArgument.Type == Nodes.Literal;
                _hasSpreads |= CanSpread(expressionArgument);
                if (expressionArgument is ArrayExpression ae)
                {
                    ref readonly var elements = ref ae.Elements;
                    for (var elementIndex = 0; elementIndex < elements.Count; elementIndex++)
                    {
                        _hasSpreads |= CanSpread(elements[elementIndex]);
                    }
                }
            }

            if (cacheable)
            {
                _cached = true;
                var arguments = Array.Empty<JsValue>();
                if (cachedArgumentsHolder.JintArguments.Length > 0)
                {
                    arguments = new JsValue[cachedArgumentsHolder.JintArguments.Length];
                    BuildArguments(context, cachedArgumentsHolder.JintArguments, arguments);
                }

                cachedArgumentsHolder.CachedArguments = arguments;
            }

            _cachedArguments = cachedArgumentsHolder;
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (!context.Engine._stackGuard.TryEnterOnCurrentStack())
            {
                return context.Engine._stackGuard.RunOnEmptyStack(EvaluateInternal, context);
            }

            if (_calleeExpression._expression.Type == Nodes.Super)
            {
                return SuperCall(context);
            }

            // https://tc39.es/ecma262/#sec-function-calls

            var reference = _calleeExpression.Evaluate(context);

            if (ReferenceEquals(reference, JsValue.Undefined))
            {
                return JsValue.Undefined;
            }

            var engine = context.Engine;
            var func = engine.GetValue(reference, false);

            if (func.IsNullOrUndefined() && _expression.IsOptional())
            {
                return JsValue.Undefined;
            }

            var referenceRecord = reference as Reference;
            if (ReferenceEquals(func, engine.Realm.Intrinsics.Eval)
                && referenceRecord != null
                && !referenceRecord.IsPropertyReference
                && referenceRecord.ReferencedName == CommonProperties.Eval)
            {
                return HandleEval(context, func, engine, referenceRecord);
            }

            var thisCall = (CallExpression) _expression;
            var tailCall = IsInTailPosition(thisCall);

            // https://tc39.es/ecma262/#sec-evaluatecall

            JsValue thisObject;
            if (referenceRecord is not null)
            {
                if (referenceRecord.IsPropertyReference)
                {
                    thisObject = referenceRecord.ThisValue;
                }
                else
                {
                    var baseValue = referenceRecord.Base;

                    // deviation from the spec to support null-propagation helper
                    if (baseValue.IsNullOrUndefined()
                        && engine._referenceResolver.TryUnresolvableReference(engine, referenceRecord, out var value))
                    {
                        thisObject = value;
                    }
                    else
                    {
                        var refEnv = (EnvironmentRecord) baseValue;
                        thisObject = refEnv.WithBaseObject();
                    }
                }
            }
            else
            {
                thisObject = JsValue.Undefined;
            }

            var arguments = ArgumentListEvaluation(context);

            if (!func.IsObject() && !engine._referenceResolver.TryGetCallable(engine, reference, out func))
            {
                ThrowMemberIsNotFunction(referenceRecord, reference, engine);
            }

            var callable = func as ICallable;
            if (callable is null)
            {
                ThrowReferenceNotFunction(referenceRecord, reference, engine);
            }

            if (tailCall)
            {
                // TODO tail call
                // PrepareForTailCall();
            }

            // ensure logic is in sync between Call, Construct and JintCallExpression!

            JsValue result;
            if (callable is FunctionInstance functionInstance)
            {
                var callStack = engine.CallStack;
                var recursionDepth = callStack.Push(functionInstance, _calleeExpression, engine.ExecutionContext);

                if (recursionDepth > engine.Options.Constraints.MaxRecursionDepth)
                {
                    // automatically pops the current element as it was never reached
                    ExceptionHelper.ThrowRecursionDepthOverflowException(callStack);
                }

                try
                {
                    result = functionInstance.Call(thisObject, arguments);
                }
                finally
                {
                    // if call stack was reset due to recursive call to engine or similar, we might not have it anymore
                    if (callStack.Count > 0)
                    {
                        callStack.Pop();
                    }
                }
            }
            else
            {
                result = callable.Call(thisObject, arguments);
            }

            if (!_cached && arguments.Length > 0)
            {
                engine._jsValueArrayPool.ReturnArray(arguments);
            }

            engine._referencePool.Return(referenceRecord);
            return result;
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowReferenceNotFunction(Reference? referenceRecord1, object reference, Engine engine)
        {
            var message = $"{referenceRecord1?.ReferencedName ?? reference} is not a function";
            ExceptionHelper.ThrowTypeError(engine.Realm, message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowMemberIsNotFunction(Reference? referenceRecord1, object reference, Engine engine)
        {
            var message = referenceRecord1 == null
                ? reference + " is not a function"
                : $"Property '{referenceRecord1.ReferencedName}' of object is not a function";
            ExceptionHelper.ThrowTypeError(engine.Realm, message);
        }

        private JsValue HandleEval(EvaluationContext context, JsValue func, Engine engine, Reference referenceRecord)
        {
            var argList = ArgumentListEvaluation(context);
            if (argList.Length == 0)
            {
                return JsValue.Undefined;
            }

            var evalFunctionInstance = (EvalFunctionInstance) func;
            var evalArg = argList[0];
            var strictCaller = StrictModeScope.IsStrictModeCode;
            var evalRealm = evalFunctionInstance._realm;
            var direct = !_expression.IsOptional();
            var value = evalFunctionInstance.PerformEval(evalArg, evalRealm, strictCaller, direct);
            engine._referencePool.Return(referenceRecord);
            return value;
        }

        private JsValue SuperCall(EvaluationContext context)
        {
            var engine = context.Engine;
            var thisEnvironment = (FunctionEnvironmentRecord) engine.ExecutionContext.GetThisEnvironment();
            var newTarget = engine.GetNewTarget(thisEnvironment);
            var func = GetSuperConstructor(thisEnvironment);
            if (func is null || !func.IsConstructor)
            {
                ExceptionHelper.ThrowTypeError(engine.Realm, "Not a constructor");
            }

            var defaultSuperCall = ReferenceEquals(_expression, ClassDefinition._defaultSuperCall);
            var argList = defaultSuperCall ? DefaultSuperCallArgumentListEvaluation(context) : ArgumentListEvaluation(context);
            var result = ((IConstructor) func).Construct(argList, newTarget);

            var thisER = (FunctionEnvironmentRecord) engine.ExecutionContext.GetThisEnvironment();
            thisER.BindThisValue(result);
            var F = thisER._functionObject;

            result.InitializeInstanceElements((ScriptFunctionInstance) F);

            return result;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getsuperconstructor
        /// </summary>
        private static ObjectInstance? GetSuperConstructor(FunctionEnvironmentRecord thisEnvironment)
        {
            var envRec = thisEnvironment;
            var activeFunction = envRec._functionObject;
            var superConstructor = activeFunction.GetPrototypeOf();
            return superConstructor;
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
            var arguments = Array.Empty<JsValue>();
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

        private JsValue[] DefaultSuperCallArgumentListEvaluation(EvaluationContext context)
        {
            // This branch behaves similarly to constructor(...args) { super(...args); }.
            // The most notable distinction is that while the aforementioned ECMAScript source text observably calls
            // the @@iterator method on %Array.prototype%, this function does not.

            var spreadExpression = (JintSpreadExpression) _cachedArguments.JintArguments[0];
            var array = (JsArray) spreadExpression._argument.GetValue(context);
            var length = array.GetLength();
            var args = new List<JsValue>((int) length);
            for (uint j = 0; j < length; ++j)
            {
                array.TryGetValue(j, out var value);
                args.Add(value);
            }

            return args.ToArray();
        }

        private sealed class CachedArgumentsHolder
        {
            internal JintExpression[] JintArguments = Array.Empty<JintExpression>();
            internal JsValue[] CachedArguments = Array.Empty<JsValue>();
        }
    }
}
