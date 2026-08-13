using System.Collections;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsSet : ObjectInstance, IEnumerable<JsValue>
{
    internal readonly OrderedSet<JsValue> _set;

    internal JsSet(Engine engine) : this(engine, new OrderedSet<JsValue>(SameValueZeroComparer.Instance))
    {
    }

    internal JsSet(Engine engine, OrderedSet<JsValue> set) : base(engine)
    {
        _set = set;
        _prototype = _engine.Realm.Intrinsics.Set.PrototypeObject;
        // Every Set reaches this constructor, subclass instances included, so the bit is exactly the
        // brand SetPrototype's methods check. It is set here rather than passed through the internal
        // ObjectInstance constructor because that one skips DeriveAccessSemantics, and this type does
        // want the OrdinaryGet the derivation gives it. See FastCallGuard.Set.
        _type |= InternalTypes.Set;
    }

    // No `size` here: it is an accessor on Set.prototype (https://tc39.es/ecma262/#sec-get-set.prototype.size)
    // and an instance has no own property of that name. See the note in JsMap for what synthesizing one cost.

    public int Size => _set.Count;

    internal JsValue? this[int index]
    {
        get { return index < _set._list.Count ? _set._list[index] : null; }
    }

    public void Add(JsValue value) => _set.Add(SameValueZeroComparer.ToStableKey(value));

    public void Clear() => _set.Clear();

    public bool Has(JsValue key) => _set.Contains(key);

    public new bool Delete(JsValue key) => _set.Remove(key);

    internal void ForEach(ICallable callable, JsValue thisArg)
    {
        var invoker = CallbackInvoker.Rent(_engine, callable, 3, this);

        var i = 0;
        var iterations = 0;
        while (i < _set._list.Count)
        {
            // A native (CLR) callback does not self-throttle via statement checks; check periodically.
            if (++iterations % Engine.ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var value = _set._list[i];
            invoker.Call(thisArg, value, value);

            // Adjust position for mutations during callback
            if (i < _set._list.Count && (ReferenceEquals(_set._list[i], value) || SameValueZeroComparer.Equals(_set._list[i], value)))
            {
                // Common fast path: value still at same position
                i++;
            }
            else if (_set.Contains(value))
            {
                var newIndex = _set.IndexOf(value);
                if (newIndex < i)
                {
                    // Value moved backward (entries before it were deleted)
                    i = newIndex + 1;
                }
                // else: value was deleted and re-added at end, keep i (entries shifted left)
            }
            // else: value was deleted, entries shifted left so i now points to next entry
        }

        invoker.Return();
    }

    internal ObjectInstance Entries() => _engine.Realm.Intrinsics.SetIteratorPrototype.ConstructEntryIterator(this);

    internal ObjectInstance Values() => _engine.Realm.Intrinsics.SetIteratorPrototype.ConstructValueIterator(this);

    public IEnumerator<JsValue> GetEnumerator() => _set.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
