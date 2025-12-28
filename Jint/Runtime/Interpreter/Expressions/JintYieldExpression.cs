using System.Collections.Generic;
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
            Throw.JavaScriptException(
                context.Engine.Intrinsics.Error,
                "Yield expressions are not supported in the engine, you can enable the experimental feature 'Generators' in engine options to use them.");
        }

        var expression = (YieldExpression) _expression;
        var generator = context.Engine.ExecutionContext.Generator;

        // Check if we're resuming from a suspended yield
        // When resuming, we need to return the value passed to next() for the yield we suspended at
        if (generator is not null && generator._isResuming)
        {
            // Check if THIS yield expression is the one we suspended at (by node identity)
            if (ReferenceEquals(_expression, generator._lastYieldNode))
            {
                // This is the yield we suspended at - return _nextValue as the result
                var returnValue = generator._nextValue ?? JsValue.Undefined;

                // Store this value for future iterations (e.g., same yield in a loop)
                generator._yieldNodeValues ??= new Dictionary<object, JsValue>();
                generator._yieldNodeValues[_expression] = returnValue;

                // Clear resume state
                generator._isResuming = false;
                generator._lastYieldNode = null;

                return returnValue;
            }

            // Check if this yield has a stored value from a previous resume
            // This happens when re-executing a loop - the same yield node was resumed before
            if (generator._yieldNodeValues?.TryGetValue(_expression, out var storedValue) == true)
            {
                return storedValue;
            }

            // This is a new yield that hasn't been processed yet
            // Fall through to normal yield logic
        }

        // Normal yield: evaluate argument and yield the value
        JsValue value;
        if (expression.Argument is not null)
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

        // Store the node we're yielding at for resume tracking
        if (generator is not null)
        {
            generator._lastYieldNode = _expression;
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
                    Throw.TypeError(engine.Realm);
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
                        Throw.TypeError(engine.Realm);
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

                    Throw.TypeError(engine.Realm, "Iterator does not have close method");
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
                    Throw.TypeError(engine.Realm);
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
        Throw.NotImplementedException("async");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratoryield
    /// </summary>
    private static Completion AsyncGeneratorYield(object iteratorValue)
    {
        Throw.NotImplementedException("async");
        return default;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#await
    /// </summary>
    private static ObjectInstance Await(JsValue innerResult)
    {
        Throw.NotImplementedException("await");
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
            Throw.NotImplementedException("async not implemented");
        }

        // https://tc39.es/ecma262/#sec-generatoryield
        var genContext = engine.ExecutionContext;
        var generator = genContext.Generator;
        generator!._generatorState = GeneratorState.SuspendedYield;
        // Store the yielded value so it can be retrieved even if the containing statement
        // has a different completion value (e.g., variable declarations return Empty)
        generator._suspendedValue = iterNextObj;

        // Throw an exception to immediately interrupt expression evaluation.
        // This is caught at the statement level to handle generator suspension properly.
        throw new YieldSuspendException(iterNextObj);
    }
}
