using Jint.Native.Array;
using Jint.Native.Iterator;
using Jint.Runtime;

namespace Jint.Native;

internal static class GroupByHelper
{
    internal static Dictionary<JsValue, JsArray> GroupBy(
        Engine engine,
        Realm realm,
        JsValue items,
        JsValue callbackfn,
        bool mapMode)
    {
        var callable = callbackfn.GetCallable(realm);
        var groups = new Dictionary<JsValue, JsArray>();
        var iteratorRecord = items.GetIterator(realm);
        new GroupByProtocol(engine, groups, iteratorRecord, callable, mapMode).Execute();
        return groups;
    }

    private sealed class GroupByProtocol : IteratorProtocol
    {
        private readonly Engine _engine;
        private readonly Dictionary<JsValue, JsArray> _result;
        private readonly bool _mapMode;
        private ulong _k;
        private readonly CallbackInvoker _invoker;

        public GroupByProtocol(
            Engine engine,
            Dictionary<JsValue, JsArray> result,
            IteratorInstance iterator,
            ICallable callable,
            bool mapMode) : base(engine, iterator, 0)
        {
            _engine = engine;
            _result = result;
            _mapMode = mapMode;
            _invoker = CallbackInvoker.Create(engine, callable, 2);
        }

        protected override void ProcessItem(JsValue[] arguments, JsValue currentValue)
        {
            if (_k >= ArrayOperations.MaxArrayLength)
            {
                Throw.TypeError(_engine.Realm);
            }

            var value = _invoker.Call(JsValue.Undefined, currentValue, _k);
            JsValue key;
            if (_mapMode)
            {
                key = (value as JsNumber)?.IsNegativeZero() == true ? JsNumber.PositiveZero : value;
            }
            else
            {
                key = TypeConverter.ToPropertyKey(value);
            }

            if (!_result.TryGetValue(key, out var list))
            {
                _result[key] = list = new JsArray(_engine);
            }

            list.Push(currentValue);
            _k++;
        }
    }
}
