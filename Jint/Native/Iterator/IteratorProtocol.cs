using Jint.Native.Array;

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

        internal void Execute()
        {
            var args = _engine._jsValueArrayPool.RentArray(_argCount);
            try
            {
                do
                {
                    var item = _iterator.Next();
                    if (item.TryGetValue("done", out var done) && done.AsBoolean())
                    {
                        break;
                    }

                    if (!item.TryGetValue("value", out var currentValue))
                    {
                        currentValue = JsValue.Undefined;
                    }

                    ProcessItem(args, currentValue);
                } while (ShouldContinue);
            }
            catch
            {
                ReturnIterator();
                throw;
            }
            finally
            {
                _engine._jsValueArrayPool.ReturnArray(args);
            }

            IterationEnd();
        }

        protected void ReturnIterator()
        {
            _iterator.Return();
        }

        protected virtual bool ShouldContinue => true;

        protected virtual void IterationEnd()
        {
        }

        protected abstract void ProcessItem(JsValue[] args, JsValue currentValue);

        protected JsValue ExtractValueFromIteratorInstance(JsValue jsValue)
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
    }
}