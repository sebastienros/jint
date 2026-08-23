using System.Collections;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsMap : ObjectInstance, IEnumerable<KeyValuePair<JsValue, JsValue>>
{
    private readonly Realm _realm;
    internal readonly KeyedCollectionData _data;

    internal JsMap(Engine engine, Realm realm) : base(engine)
    {
        _realm = realm;
        _data = new KeyedCollectionData();
        // Every Map reaches this constructor, subclass instances included, so the bit is exactly the
        // brand MapPrototype's methods check. It is set here rather than passed through the internal
        // ObjectInstance constructor because that one skips DeriveAccessSemantics, and this type does
        // want the OrdinaryGet the derivation gives it. See FastCallGuard.Map.
        _type |= InternalTypes.Map;
    }

    // No `size` here: it is an accessor on Map.prototype (https://tc39.es/ecma262/#sec-get-map.prototype.size)
    // and an instance has no own property of that name. Synthesizing one from GetOwnProperty made the
    // prototype getter unreachable through `m.size` — which is how it went on returning a hard-coded 0 — and
    // gave every map a phantom own non-configurable `size` that [[OwnPropertyKeys]] never listed.

    public int Size => _data.Count;

    public void Clear() => _data.Clear();

    public bool Has(JsValue key) => _data.ContainsKey(key);

    public bool Remove(JsValue key) => _data.Remove(key);

    public new JsValue Get(JsValue key)
    {
        if (!_data.TryGetValue(key, out var value))
        {
            return Undefined;
        }

        return value;
    }

    internal JsValue GetOrInsert(JsValue key, JsValue value)
    {
        key = SameValueZeroComparer.ToStableKey(key);
        if (_data.TryGetValue(key, out var temp))
        {
            return temp;
        }

        _data.Append(key, value);
        return value;
    }

    internal JsValue GetOrInsertComputed(JsValue key, ICallable callbackfn)
    {
        // Flatten before the callback runs: the key is the string *value* the operation was handed
        // (spec step 4 canonicalizes it up front), not a buffer the callback may still append to.
        key = SameValueZeroComparer.ToStableKey(key);
        if (_data.TryGetValue(key, out var temp))
        {
            return temp;
        }

        var value = callbackfn.Call(Undefined, key);

        // The callback may have inserted the key itself, so this cannot assume it is still absent.
        _data.Set(key, value);
        return value;
    }

    public new void Set(JsValue key, JsValue value)
    {
        if (key is JsNumber number && number.IsNegativeZero())
        {
            key = JsNumber.PositiveZero;
        }
        _data.Set(SameValueZeroComparer.ToStableKey(key), value);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-map.prototype.foreach
    /// </summary>
    internal void ForEach(ICallable callable, JsValue thisArg)
    {
        var invoker = CallbackInvoker.Rent(_engine, callable, 3, this);

        // See JsSet.ForEach: the cursor is the spec's `index` into [[MapData]] and stays exact across
        // whatever the callback does to the map.
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

            invoker.Call(thisArg, _data.ValueAt(slot), _data.KeyAt(slot)!);
        }

        invoker.Return();
    }

    internal ObjectInstance Iterator() => _realm.Intrinsics.MapIteratorPrototype.ConstructEntryIterator(this);

    internal ObjectInstance Keys() => _realm.Intrinsics.MapIteratorPrototype.ConstructKeyIterator(this);

    internal ObjectInstance Values() => _realm.Intrinsics.MapIteratorPrototype.ConstructValueIterator(this);

    public IEnumerator<KeyValuePair<JsValue, JsValue>> GetEnumerator()
    {
        var enumerator = _data.GetEnumerator();
        while (enumerator.MoveNext())
        {
            yield return new KeyValuePair<JsValue, JsValue>(enumerator.Key, enumerator.Value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
