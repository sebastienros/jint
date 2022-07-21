using System.Runtime.CompilerServices;

using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.WeakSet;

public sealed class WeakSetInstance : ObjectInstance
{
    private readonly ConditionalWeakTable<JsValue, JsValue> _table;

    public WeakSetInstance(Engine engine) : base(engine)
    {
        _table = new ConditionalWeakTable<JsValue, JsValue>();
    }

    internal bool WeakSetHas(JsValue value)
    {
        return _table.TryGetValue(value, out _);
    }

    internal bool WeakSetDelete(JsValue value)
    {
        return _table.Remove(value);
    }

    internal void WeakSetAdd(JsValue value)
    {
        if (value.IsPrimitive())
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "WeakSet value must be an object, got " + value);
        }

#if NETSTANDARD2_1_OR_GREATER
        _table.AddOrUpdate(value, Undefined);
#else
        _table.Remove(value);
        _table.Add(value, Undefined);
#endif
    }

}
