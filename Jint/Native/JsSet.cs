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

        for (var i = 0; i < _set._list.Count; i++)
        {
            var value = _set._list[i];
            args[0] = value;
            args[1] = value;
            callable.Call(thisArg, args);
        }

        _engine._jsValueArrayPool.ReturnArray(args);
    }

    internal ObjectInstance Entries() => _engine.Realm.Intrinsics.SetIteratorPrototype.ConstructEntryIterator(this);

    internal ObjectInstance Values() => _engine.Realm.Intrinsics.SetIteratorPrototype.ConstructValueIterator(this);

    public IEnumerator<JsValue> GetEnumerator() => _set.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
