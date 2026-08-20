using System.Collections;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsSet : ObjectInstance, IEnumerable<JsValue>
{
    internal readonly KeyedCollectionData _data;

    internal JsSet(Engine engine) : this(engine, new KeyedCollectionData())
    {
    }

    internal JsSet(Engine engine, KeyedCollectionData data) : base(engine)
    {
        _data = data;
        _prototype = _engine.Realm.Intrinsics.Set.PrototypeObject;
        // Every Set reaches this constructor, subclass instances included, so the bit is exactly the
        // brand SetPrototype's methods check. It is set here rather than passed through the internal
        // ObjectInstance constructor because that one skips DeriveAccessSemantics, and this type does
        // want the OrdinaryGet the derivation gives it. See FastCallGuard.Set.
        _type |= InternalTypes.Set;
    }

    // No `size` here: it is an accessor on Set.prototype (https://tc39.es/ecma262/#sec-get-set.prototype.size)
    // and an instance has no own property of that name. See the note in JsMap for what synthesizing one cost.

    public int Size => _data.Count;

    public void Add(JsValue value) => _data.Add(SameValueZeroComparer.ToStableKey(value));

    public void Clear() => _data.Clear();

    public bool Has(JsValue key) => _data.ContainsKey(key);

    public new bool Delete(JsValue key) => _data.Remove(key);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.foreach
    /// </summary>
    internal void ForEach(ICallable callable, JsValue thisArg)
    {
        var invoker = CallbackInvoker.Rent(_engine, callable, 3, this);

        // The cursor is the spec's `index` into [[SetData]]: the callback may add, delete and re-add
        // freely, and the tombstoned representation keeps the resume point exact without any of the
        // relocate-the-last-value guesswork a compacting list needed.
        var cursor = default(KeyedCollectionCursor);
        var iterations = 0;
        int slot;
        while ((slot = _data.Next(ref cursor)) >= 0)
        {
            // A native (CLR) callback does not self-throttle via statement checks; check periodically.
            if (++iterations % Engine.ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var value = _data.KeyAt(slot)!;
            invoker.Call(thisArg, value, value);
        }

        invoker.Return();
    }

    internal ObjectInstance Entries() => _engine.Realm.Intrinsics.SetIteratorPrototype.ConstructEntryIterator(this);

    internal ObjectInstance Values() => _engine.Realm.Intrinsics.SetIteratorPrototype.ConstructValueIterator(this);

    public IEnumerator<JsValue> GetEnumerator()
    {
        var enumerator = _data.GetEnumerator();
        while (enumerator.MoveNext())
        {
            yield return enumerator.Key;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
