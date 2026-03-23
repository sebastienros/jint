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
        var instance = new SetValueIterator(Engine, set);
        return instance;
    }

    private sealed class SetEntryIterator : IteratorInstance
    {
        private readonly JsSet _set;
        private int _position;
        private JsValue? _lastValue;
        private bool _done;

        public SetEntryIterator(Engine engine, JsSet set) : base(engine)
        {
            _prototype = engine.Realm.Intrinsics.SetIteratorPrototype;
            _set = set;
            _position = 0;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (_done)
            {
                nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
                return false;
            }

            // Adjust position for mutations since last step
            AdjustPosition(ref _position, _lastValue, _set._set);

            if (_position < _set._set._list.Count)
            {
                var value = _set._set[_position];
                _lastValue = value;
                _position++;
                nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine, value, value);
                return true;
            }

            _done = true;
            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }

    private sealed class SetValueIterator : IteratorInstance
    {
        private readonly JsSet _set;
        private int _position;
        private JsValue? _lastValue;
        private bool _done;

        public SetValueIterator(Engine engine, JsSet set) : base(engine)
        {
            _prototype = engine.Realm.Intrinsics.SetIteratorPrototype;
            _set = set;
            _position = 0;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (_done)
            {
                nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
                return false;
            }

            // Adjust position for mutations since last step
            AdjustPosition(ref _position, _lastValue, _set._set);

            if (_position < _set._set._list.Count)
            {
                var value = _set._set[_position];
                _lastValue = value;
                _position++;
                nextItem = IteratorResult.CreateValueIteratorPosition(_engine, value);
                return true;
            }

            _done = true;
            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }

    private static void AdjustPosition(ref int position, JsValue? lastValue, OrderedSet<JsValue> set)
    {
        if (lastValue is null)
        {
            return;
        }

        if (position > 0 && position - 1 < set._list.Count && ReferenceEquals(set._list[position - 1], lastValue))
        {
            // Common fast path: lastValue still at expected position
        }
        else if (set.Contains(lastValue))
        {
            var newIndex = set._list.IndexOf(lastValue);
            if (newIndex < position - 1)
            {
                // Value moved backward (entries before it were deleted)
                position = newIndex + 1;
            }
            // else: value was deleted and re-added at end, keep position
        }
        else
        {
            // Value deleted, entries shifted left
            position = System.Math.Max(0, position - 1);
        }
    }
}
