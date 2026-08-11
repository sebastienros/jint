using System.Collections;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsMap : ObjectInstance, IEnumerable<KeyValuePair<JsValue, JsValue>>
{
    private readonly Realm _realm;
    internal readonly JintOrderedDictionary<JsValue, JsValue> _map;

    public JsMap(Engine engine, Realm realm) : base(engine)
    {
        _realm = realm;
        _map = new JintOrderedDictionary<JsValue, JsValue>(SameValueZeroComparer.Instance);
    }

    // No `size` here: it is an accessor on Map.prototype (https://tc39.es/ecma262/#sec-get-map.prototype.size)
    // and an instance has no own property of that name. Synthesizing one from GetOwnProperty made the
    // prototype getter unreachable through `m.size` — which is how it went on returning a hard-coded 0 — and
    // gave every map a phantom own non-configurable `size` that [[OwnPropertyKeys]] never listed.

    public int Size => _map.Count;

    public void Clear() => _map.Clear();

    public bool Has(JsValue key) => _map.ContainsKey(key);

    public bool Remove(JsValue key) => _map.Remove(key);

    public new JsValue Get(JsValue key)
    {
        if (!_map.TryGetValue(key, out var value))
        {
            return Undefined;
        }

        return value;
    }

    internal JsValue GetOrInsert(JsValue key, JsValue value)
    {
        key = SameValueZeroComparer.ToStableKey(key);
        if (_map.TryGetValue(key, out var temp))
        {
            return temp;
        }

        _map[key] = value;
        return value;
    }

    internal JsValue GetOrInsertComputed(JsValue key, ICallable callbackfn)
    {
        // Flatten before the callback runs: the key is the string *value* the operation was handed
        // (spec step 4 canonicalizes it up front), not a buffer the callback may still append to.
        key = SameValueZeroComparer.ToStableKey(key);
        if (_map.TryGetValue(key, out var temp))
        {
            return temp;
        }

        var value = callbackfn.Call(Undefined, key);

        _map[key] = value;
        return value;
    }

    public new void Set(JsValue key, JsValue value)
    {
        if (key is JsNumber number && number.IsNegativeZero())
        {
            key = JsNumber.PositiveZero;
        }
        _map[SameValueZeroComparer.ToStableKey(key)] = value;
    }

    internal void ForEach(ICallable callable, JsValue thisArg)
    {
        var invoker = CallbackInvoker.Rent(_engine, callable, 3, this);

        var i = 0;
        var iterations = 0;
        while (i < _map.Count)
        {
            // A native (CLR) callback does not self-throttle via statement checks; check periodically.
            if (++iterations % Engine.ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var key = _map.GetKey(i);
            invoker.Call(thisArg, _map[key], key);

            // Adjust position for mutations during callback
            if (i < _map.Count && ReferenceEquals(_map.GetKey(i), key))
            {
                // Common fast path: key still at same position
                i++;
            }
            else if (_map.ContainsKey(key))
            {
                var newIndex = _map.IndexOf(key);
                if (newIndex < i)
                {
                    // Key moved backward (entries before it were deleted)
                    i = newIndex + 1;
                }
                // else: key was deleted and re-added at end, keep i (entries shifted left)
            }
            // else: key was deleted, entries shifted left so i now points to next entry
        }

        invoker.Return();
    }

    internal ObjectInstance Iterator() => _realm.Intrinsics.MapIteratorPrototype.ConstructEntryIterator(this);

    internal ObjectInstance Keys() => _realm.Intrinsics.MapIteratorPrototype.ConstructKeyIterator(this);

    internal ObjectInstance Values() => _realm.Intrinsics.MapIteratorPrototype.ConstructValueIterator(this);

    public IEnumerator<KeyValuePair<JsValue, JsValue>> GetEnumerator() => _map.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
