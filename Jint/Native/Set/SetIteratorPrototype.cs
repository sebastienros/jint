using Jint.Collections;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Set;

/// <summary>
/// https://tc39.es/ecma262/#sec-%setiteratorprototype%-object
/// </summary>
internal sealed class SetIteratorPrototype : IteratorPrototype
{
    internal SetIteratorPrototype(
        Engine engine,
        Realm realm,
        IteratorPrototype iteratorPrototype) : base(engine, realm, iteratorPrototype)
    {
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            [KnownKeys.Next] = new(new ClrFunction(Engine, "next", Next, 0, PropertyFlag.Configurable), true, false, true)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Set Iterator", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    internal IteratorInstance ConstructEntryIterator(JsSet set)
    {
        var instance = new SetEntryIterator(Engine, set);
        return instance;
    }

    internal IteratorInstance ConstructValueIterator(JsSet set)
    {
        var instance = new SetValueIterator(Engine, set._set._list);
        return instance;
    }

    private sealed class SetEntryIterator : IteratorInstance
    {
        private readonly JsSet _set;
        private int _position;

        public SetEntryIterator(Engine engine, JsSet set) : base(engine)
        {
            _prototype = engine.Realm.Intrinsics.SetIteratorPrototype;
            _set = set;
            _position = 0;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (_position < _set._set._list.Count)
            {
                var value = _set._set[_position];
                _position++;
                nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine, value, value);
                return true;
            }

            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }

    private sealed class SetValueIterator : IteratorInstance
    {
        private readonly List<JsValue> _values;
        private int _position;
        private bool _closed;

        public SetValueIterator(Engine engine, List<JsValue> values) : base(engine)
        {
            _prototype = engine.Realm.Intrinsics.SetIteratorPrototype;
            _values = values;
            _position = 0;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (!_closed && _position < _values.Count)
            {
                var value = _values[_position];
                _position++;
                nextItem = IteratorResult.CreateValueIteratorPosition(_engine, value);
                return true;
            }

            _closed = true;
            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }
}
