using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Set;

/// <summary>
/// https://tc39.es/ecma262/#sec-%setiteratorprototype%-object
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class SetIteratorPrototype : IteratorPrototype
{
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString SetIteratorToStringTag = new("Set Iterator");

    internal SetIteratorPrototype(
        Engine engine,
        Realm realm,
        IteratorPrototype iteratorPrototype) : base(engine, realm, iteratorPrototype)
    {
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    [JsFunction(Name = "next")]
    private JsValue NextHandler(JsValue thisObject) => Next(thisObject, Arguments.Empty);

    internal IteratorInstance ConstructEntryIterator(JsSet set)
    {
        var instance = new SetIterator(Engine, set, keyAndValue: true);
        return instance;
    }

    internal IteratorInstance ConstructValueIterator(JsSet set)
    {
        var instance = new SetIterator(Engine, set, keyAndValue: false);
        return instance;
    }

    /// <summary>
    /// The closure of https://tc39.es/ecma262/#sec-createsetiterator, which walks [[SetData]] by index
    /// and re-reads its length after every yield. <see cref="KeyedCollectionCursor"/> is that index:
    /// deleted entries are tombstones, so a mutation between two steps cannot move the resume point.
    /// </summary>
    private sealed class SetIterator : IteratorInstance
    {
        private readonly KeyedCollectionData _data;
        private readonly bool _keyAndValue;
        private KeyedCollectionCursor _cursor;
        private bool _done;

        public SetIterator(Engine engine, JsSet set, bool keyAndValue) : base(engine)
        {
            _prototype = engine.Realm.Intrinsics.SetIteratorPrototype;
            _data = set._data;
            _keyAndValue = keyAndValue;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (!_done)
            {
                var slot = _data.Next(ref _cursor);
                if (slot >= 0)
                {
                    var value = _data.KeyAt(slot)!;
                    nextItem = _keyAndValue
                        ? IteratorResult.CreateKeyValueIteratorPosition(_engine, value, value)
                        : IteratorResult.CreateValueIteratorPosition(_engine, value);
                    return true;
                }

                _done = true;
            }

            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }
}
