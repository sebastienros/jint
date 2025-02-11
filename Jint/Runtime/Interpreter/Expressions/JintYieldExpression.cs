using Jint.Native;
using Jint.Native.Generator;
using Jint.Native.Iterator;
using Jint.Native.Object;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintYieldExpression : JintExpression
{
    public JintYieldExpression(YieldExpression expression) : base(expression)
    {
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if ((context.Engine.Options.ExperimentalFeatures & ExperimentalFeature.Generators) == ExperimentalFeature.None)
        {
            ExceptionHelper.ThrowJavaScriptException(
                context.Engine.Intrinsics.Error,
                "Yield expressions are not supported in the engine, you can enable the experimental feature 'Generators' in engine options to use them.");
        }

        var expression = (YieldExpression) _expression;

        JsValue value;
        if (context.Engine.ExecutionContext.Generator?._nextValue is not null)
        {
            value = context.Engine.ExecutionContext.Generator._nextValue;
        }
        else if (expression.Argument is not null)
        {
            value = Build(expression.Argument).GetValue(context);
        }
        else
        {
            value = JsValue.Undefined;
        }

        if (expression.Delegate)
        {
            value = YieldDelegate(context, value);
        }

        return Yield(context, value);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation
    /// </summary>
    private JsValue YieldDelegate(EvaluationContext context, JsValue value)
    {
        var engine = context.Engine;
        var generatorKind = engine.ExecutionContext.GetGeneratorKind();
        var iterator = value.GetIterator(engine.Realm, generatorKind);
        var iteratorRecord = iterator;
        var received = new Completion(CompletionType.Normal, JsValue.Undefined, _expression);
        while (true)
        {
            if (received.Type == CompletionType.Normal)
            {
                iterator.TryIteratorStep(out var innerResult);
                if (generatorKind == GeneratorKind.Async)
                {
                    innerResult = Await(innerResult);
                }

                if (innerResult is not IteratorResult oi)
                {
                    ExceptionHelper.ThrowTypeError(engine.Realm);
                }

                var done = IteratorComplete(innerResult);
                if (done)
                {
                    return IteratorValue(innerResult);
                }

                if (generatorKind == GeneratorKind.Async)
                {
                    received = AsyncGeneratorYield(IteratorValue(innerResult));
                }
                else
                {
                    received = GeneratorYield(innerResult);
                }

            }
            else if (received.Type == CompletionType.Throw)
            {
                var throwMethod = iterator.GetMethod("throw");
                if (throwMethod is not null)
                {
                    var innerResult = throwMethod.Call(iterator, received.Value);
                    if (generatorKind == GeneratorKind.Async)
                    {
                        innerResult = Await(innerResult);
                    }
                    // NOTE: Exceptions from the inner iterator throw method are propagated.
                    // Normal completions from an inner throw method are processed similarly to an inner next.
                    if (innerResult is not ObjectInstance oi)
                    {
                        ExceptionHelper.ThrowTypeError(engine.Realm);
                    }

                    var done = IteratorComplete(innerResult);
                    if (done)
                    {
                        IteratorValue(innerResult);
                    }

                    if (generatorKind == GeneratorKind.Async)
                    {
                        received = AsyncGeneratorYield(IteratorValue(innerResult));
                    }
                    else
                    {
                        received = GeneratorYield(innerResult);
                    }
                }
                else
                {
                    // NOTE: If iterator does not have a throw method, this throw is going to terminate the yield* loop.
                    // But first we need to give iterator a chance to clean up.
                    var closeCompletion = new Completion(CompletionType.Normal, null!, _expression);
                    if (generatorKind == GeneratorKind.Async)
                    {
                        AsyncIteratorClose(iteratorRecord, CompletionType.Normal);
                    }
                    else
                    {
                        iteratorRecord.Close(CompletionType.Normal);
                    }

                    ExceptionHelper.ThrowTypeError(engine.Realm, "Iterator does not have close method");
                }
            }
            else
            {
                var returnMethod = iterator.GetMethod("return");
                if (returnMethod is null)
                {
                    var temp = received.Value;
                    if (generatorKind == GeneratorKind.Async)
                    {
                        temp = Await(received.Value);
                    }

                    return temp;
                }

                var innerReturnResult = returnMethod.Call(iterator, received.Value);
                if (generatorKind == GeneratorKind.Async)
                {
                    innerReturnResult = Await(innerReturnResult);
                }

                if (innerReturnResult is not ObjectInstance oi)
                {
                    ExceptionHelper.ThrowTypeError(engine.Realm);
                }

                var done = IteratorComplete(innerReturnResult);
                if (done)
                {
                    var val = IteratorValue(innerReturnResult);
                    return val;
                }

                if (generatorKind == GeneratorKind.Async)
                {
                    received = AsyncGeneratorYield(IteratorValue(innerReturnResult));
                }
                else
                {
                    received = GeneratorYield(innerReturnResult);
                }
            }
        }
    }

    private Completion GeneratorYield(JsValue innerResult)
    {
        throw new System.NotImplementedException();
    }

    private static bool IteratorComplete(JsValue iterResult)
    {
        return TypeConverter.ToBoolean(iterResult.Get(CommonProperties.Done));
    }

    private static JsValue IteratorValue(JsValue iterResult)
    {
        return iterResult.Get(CommonProperties.Value);
    }

    private static void AsyncIteratorClose(object iteratorRecord, CompletionType closeCompletion)
    {
        ExceptionHelper.ThrowNotImplementedException("async");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratoryield
    /// </summary>
    private static Completion AsyncGeneratorYield(object iteratorValue)
    {
        ExceptionHelper.ThrowNotImplementedException("async");
        return default;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#await
    /// </summary>
    private static ObjectInstance Await(JsValue innerResult)
    {
        ExceptionHelper.ThrowNotImplementedException("await");
        return null;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-yield
    /// </summary>
    private static JsValue Yield(EvaluationContext context, JsValue iterNextObj)
    {
        var engine = context.Engine;
        var generatorKind = engine.ExecutionContext.GetGeneratorKind();
        if (generatorKind == GeneratorKind.Async)
        {
            // TODO return ? AsyncGeneratorYield(undefined);
            ExceptionHelper.ThrowNotImplementedException("async not implemented");
        }

        // https://tc39.es/ecma262/#sec-generatoryield
        var genContext = engine.ExecutionContext;
        var generator = genContext.Generator;
        generator!._generatorState = GeneratorState.SuspendedYield;
        //_engine.LeaveExecutionContext();

        return iterNextObj;
    }
}
