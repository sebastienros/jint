using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Iterator;

/// <summary>
/// Handles looping of iterator values, sub-classes can use to implement wanted actions.
/// </summary>
internal abstract class IteratorProtocol
{
    private readonly Engine _engine;
    private readonly IteratorInstance _iterator;
    private readonly int _argCount;

    protected IteratorProtocol(
        Engine engine,
        IteratorInstance iterator,
        int argCount)
    {
        _engine = engine;
        _iterator = iterator;
        _argCount = argCount;
    }

    internal bool Execute()
    {
        var args = _engine._jsValueArrayPool.RentArray(_argCount);
        var done = false;
        var iterations = 0;
        try
        {
            while (ShouldContinue)
            {
                // Check constraints periodically so a huge (or native-backed) iterator cannot run
                // uninterrupted; the surrounding try closes the iterator and returns the pool array.
                if (++iterations % Engine.ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }

                if (!_iterator.TryIteratorStepValue(out var currentValue))
                {
                    done = true;
                    break;
                }

                ProcessItem(args, currentValue);
            }
        }
        catch
        {
            // Only ProcessItem is the caller's own abrupt completion; a failure inside the step has
            // set the record's [[Done]] and must propagate unclosed (IteratorStepValue).
            _iterator.CloseIfNotDone(CompletionType.Throw);
            throw;
        }
        finally
        {
            _engine._jsValueArrayPool.ReturnArray(args);
        }

        IterationEnd();
        return done;
    }

    protected void IteratorClose(CompletionType completionType)
    {
        _iterator.Close(completionType);
    }

    protected virtual bool ShouldContinue => true;

    protected virtual void IterationEnd()
    {
    }

    protected abstract void ProcessItem(JsValue[] arguments, JsValue currentValue);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-add-entries-from-iterable
    /// </summary>
    internal static void AddEntriesFromIterable(ObjectInstance target, IteratorInstance iterable, object adder)
    {
        var callable = adder as ICallable;
        if (callable is null)
        {
            Throw.TypeError(target.Engine.Realm, "adder must be callable");
        }

        var invoker = CallbackInvoker.Rent(target.Engine, callable, 2);

        var iterations = 0;
        try
        {
            do
            {
                // Check constraints periodically so a huge (or native-backed) iterable cannot run
                // uninterrupted (covers new Map/WeakMap and Object.fromEntries).
                if (++iterations % Engine.ConstraintCheckInterval == 0)
                {
                    target.Engine.Constraints.Check();
                }

                if (!iterable.TryIteratorStepValue(out var temp))
                {
                    return;
                }

                var oi = temp as ObjectInstance;
                if (oi is null)
                {
                    Throw.TypeError(target.Engine.Realm, "iterator's value must be an object");
                }

                var k = oi.Get(JsString.NumberZeroString);
                var v = oi.Get(JsString.NumberOneString);

                invoker.Call(target, k, v);
            } while (true);
        }
        catch
        {
            // Steps 2.c/2.e/2.g/2.i close; step 2.a's own abrupt completion does not, and [[Done]] is
            // what tells the two apart on every iteration, not just the first.
            iterable.CloseIfNotDone(CompletionType.Throw);
            throw;
        }
        finally
        {
            invoker.Return();
        }
    }
}
