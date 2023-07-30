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
        private readonly ICallable _callable;
        private readonly bool _mapMode;
        private ulong _k;
        private readonly JsValue[] _callArgs = new JsValue[2];

        public GroupByProtocol(
            Engine engine,
            Dictionary<JsValue, JsArray> result,
            IteratorInstance iterator,
            ICallable callable,
            bool mapMode) : base(engine, iterator, 0)
        {
            _engine = engine;
            _result = result;
            _callable = callable;
            _mapMode = mapMode;
        }

        protected override void ProcessItem(JsValue[] arguments, JsValue currentValue)
        {
            if (_k >= ArrayOperations.MaxArrayLength)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }

            _callArgs[0] = currentValue;
            _callArgs[1] = _k;

            var value = _callable.Call(JsValue.Undefined, _callArgs);
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
