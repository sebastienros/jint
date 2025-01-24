using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.CallStack;
using Jint.Runtime.Environments;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintCallExpression : JintExpression
{
    private readonly ExpressionCache _arguments = new();
    private JintExpression _calleeExpression = null!;
    private bool _initialized;

    public JintCallExpression(CallExpression expression) : base(expression)
    {
    }

    private void Initialize(EvaluationContext context)
    {
        var expression = (CallExpression) _expression;
        _arguments.Initialize(context, expression.Arguments.AsSpan());
        _calleeExpression = Build(expression.Callee);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            Initialize(context);
            _initialized = true;
        }

        if (!context.Engine._stackGuard.TryEnterOnCurrentStack())
        {
            return StackGuard.RunOnEmptyStack(EvaluateInternal, context);
        }

        if (_calleeExpression._expression.Type == NodeType.Super)
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
            && CommonProperties.Eval.Equals(referenceRecord.ReferencedName))
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
                    var refEnv = (Environment) baseValue;
                    thisObject = refEnv.WithBaseObject();
                }
            }
        }
        else
        {
            thisObject = JsValue.Undefined;
        }

        var arguments = this._arguments.ArgumentListEvaluation(context, out var rented);

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
        if (callable is Function functionInstance)
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

        if (rented)
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
        var argList = _arguments.ArgumentListEvaluation(context, out var rented);

        if (argList.Length == 0)
        {
            return JsValue.Undefined;
        }

        var evalFunctionInstance = (EvalFunction) func;
        var evalArg = argList[0];
        var strictCaller = StrictModeScope.IsStrictModeCode;
        var evalRealm = evalFunctionInstance._realm;
        var direct = !_expression.IsOptional();
        var value = evalFunctionInstance.PerformEval(evalArg, evalRealm, strictCaller, direct);

        if (rented)
        {
            engine._jsValueArrayPool.ReturnArray(argList);
        }
        engine._referencePool.Return(referenceRecord);

        return value;
    }

    private ObjectInstance SuperCall(EvaluationContext context)
    {
        var engine = context.Engine;
        var thisEnvironment = (FunctionEnvironment) engine.ExecutionContext.GetThisEnvironment();
        var newTarget = engine.GetNewTarget(thisEnvironment);
        var func = GetSuperConstructor(thisEnvironment);
        if (func is null || !func.IsConstructor)
        {
            ExceptionHelper.ThrowTypeError(engine.Realm, "Not a constructor");
        }

        var rented = false;
        var defaultSuperCall = ReferenceEquals(_expression, ClassDefinition._defaultSuperCall);

        var argList = defaultSuperCall
            ? _arguments.DefaultSuperCallArgumentListEvaluation(context)
            : _arguments.ArgumentListEvaluation(context, out rented);

        var result = ((IConstructor) func).Construct(argList, newTarget);

        var thisER = (FunctionEnvironment) engine.ExecutionContext.GetThisEnvironment();
        thisER.BindThisValue(result);
        var F = thisER._functionObject;

        result.InitializeInstanceElements((ScriptFunction) F);

        if (rented)
        {
            engine._jsValueArrayPool.ReturnArray(argList);
        }

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getsuperconstructor
    /// </summary>
    private static ObjectInstance? GetSuperConstructor(FunctionEnvironment thisEnvironment)
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
}
