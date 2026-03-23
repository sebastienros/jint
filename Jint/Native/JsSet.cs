using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

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
    }

    public int Size => _set.Count;

    internal JsValue? this[int index]
    {
        get { return index < _set._list.Count ? _set._list[index] : null; }
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (CommonProperties.Size.Equals(property))
        {
            return new PropertyDescriptor(_set.Count, PropertyFlag.AllForbidden);
        }

        return base.GetOwnProperty(property);
    }

    protected override bool TryGetProperty(JsValue property, [NotNullWhen(true)] out PropertyDescriptor? descriptor)
    {
        if (CommonProperties.Size.Equals(property))
        {
            descriptor = new PropertyDescriptor(_set.Count, PropertyFlag.AllForbidden);
            return true;
        }

        return base.TryGetProperty(property, out descriptor);
    }

    public void Add(JsValue value) => _set.Add(value);

    public void Clear() => _set.Clear();

    public bool Has(JsValue key) => _set.Contains(key);

    public new bool Delete(JsValue key) => _set.Remove(key);

    internal void ForEach(ICallable callable, JsValue thisArg)
    {
        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = this;

        var i = 0;
        while (i < _set._list.Count)
        {
            var value = _set._list[i];
            args[0] = value;
            args[1] = value;
            callable.Call(thisArg, args);

            // Adjust position for mutations during callback
            if (i < _set._list.Count && SameComparison(_set._list[i], value))
            {
                // Common fast path: value still at same position
                i++;
            }
            else if (_set.Contains(value))
            {
                var newIndex = _set._list.IndexOf(value);
                if (newIndex < i)
                {
                    // Value moved backward (entries before it were deleted)
                    i = newIndex + 1;
                }
                // else: value was deleted and re-added at end, keep i (entries shifted left)
            }
            // else: value was deleted, entries shifted left so i now points to next entry
        }

        _engine._jsValueArrayPool.ReturnArray(args);
    }

    private static bool SameComparison(JsValue a, JsValue b)
    {
        // Use reference equality for most values, SameValueZero for numbers
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        // Handle the case where JsNumber instances may not be reference equal
        if (a is JsNumber na && b is JsNumber nb)
        {
            return na._value == nb._value || (double.IsNaN(na._value) && double.IsNaN(nb._value));
        }

        return false;
    }

    internal ObjectInstance Entries() => _engine.Realm.Intrinsics.SetIteratorPrototype.ConstructEntryIterator(this);

    internal ObjectInstance Values() => _engine.Realm.Intrinsics.SetIteratorPrototype.ConstructValueIterator(this);

    public IEnumerator<JsValue> GetEnumerator() => _set.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
