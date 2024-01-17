using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

internal sealed class JsWeakSet : ObjectInstance
{
    private readonly ConditionalWeakTable<JsValue, JsValue> _table;

    public JsWeakSet(Engine engine) : base(engine)
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
        if (!value.CanBeHeldWeakly(_engine.GlobalSymbolRegistry))
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "WeakSet value must be an object or symbol, got " + value);
        }

#if SUPPORTS_WEAK_TABLE_ADD_OR_UPDATE
        _table.AddOrUpdate(value, Undefined);
#else
        _table.Remove(value);
        _table.Add(value, Undefined);
#endif
    }
}
