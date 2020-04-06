using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Iterator
{
    /// <summary>
    /// Handles looping of iterator values, sub-classes can use to implement wanted actions.
    /// </summary>
    internal abstract class IteratorProtocol
    {
        protected readonly Engine _engine;
        private readonly IIterator _iterator;
        private readonly int _argCount;

        protected IteratorProtocol(
            Engine engine,
            IIterator iterator,
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
            try
            {
                do
                {
                    if (!_iterator.TryIteratorStep(out var item))
                    {
                        done = true;
                        break;
                    }

                    var currentValue = item.Get(CommonProperties.Value);

                    ProcessItem(args, currentValue);
                } while (ShouldContinue);
            }
            catch
            {
                IteratorClose(CompletionType.Throw);
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

        protected abstract void ProcessItem(JsValue[] args, JsValue currentValue);

        protected static JsValue ExtractValueFromIteratorInstance(JsValue jsValue)
        {
            if (jsValue is ArrayInstance ai)
            {
                uint index = 0;
                if (ai.GetLength() > 1)
                {
                    index = 1;
                }

                ai.TryGetValue(index, out var value);
                return value;
            }

            return jsValue;
        }

        internal static void AddEntriesFromIterable(ObjectInstance target, IIterator iterable, object adder)
        {
            if (!(adder is ICallable callable))
            {
                ExceptionHelper.ThrowTypeError(target.Engine, "adder must be callable");
                return;
            }

            var args = target.Engine._jsValueArrayPool.RentArray(2);

            var skipClose = true;
            try
            {
                do
                {
                    if (!iterable.TryIteratorStep(out var nextItem))
                    {
                        return;
                    }

                    var temp = nextItem.Get(CommonProperties.Value);

                    skipClose = false;
                    if (!(temp is ObjectInstance oi))
                    {
                        ExceptionHelper.ThrowTypeError(target.Engine, "iterator's value must be an object");
                        return;
                    }

                    var k = oi.Get(JsString.NumberZeroString);
                    var v = oi.Get(JsString.NumberOneString);

                    args[0] = k;
                    args[1] = v;

                    callable.Call(target, args);
                } while (true);
            }
            catch
            {
                if (!skipClose)
                {
                    iterable.Close(CompletionType.Throw);
                }
                throw;
            }
            finally
            {
                target.Engine._jsValueArrayPool.ReturnArray(args);
            }
        }
    }
}