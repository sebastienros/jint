using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Iterator
{
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
            var done = false;
            try
            {
                while (ShouldContinue)
                {
                    if (!_iterator.TryIteratorStep(out var item))
                    {
                        done = true;
                        break;
                    }

                    var currentValue = item.Get(CommonProperties.Value);

                    ProcessItem(currentValue);
                }
            }
            catch
            {
                IteratorClose(CompletionType.Throw);
                throw;
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

        protected abstract void ProcessItem(JsValue currentValue);

        internal static void AddEntriesFromIterable(ObjectInstance target, IteratorInstance iterable, object adder)
        {
            var callable = adder as ICallable;
            if (callable is null)
            {
                ExceptionHelper.ThrowTypeError(target.Engine.Realm, "adder must be callable");
            }

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
                    var oi = temp as ObjectInstance;
                    if (oi is null)
                    {
                        ExceptionHelper.ThrowTypeError(target.Engine.Realm, "iterator's value must be an object");
                    }

                    var k = oi.Get(JsString.NumberZeroString);
                    var v = oi.Get(JsString.NumberOneString);

                    callable.Call(target, new Arguments(k, v));
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
        }
    }
}
