using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

internal sealed class JsWeakMap : ObjectInstance
{
    private readonly ConditionalWeakTable<JsValue, JsValue> _table;

    public JsWeakMap(Engine engine) : base(engine)
    {
        _table = new ConditionalWeakTable<JsValue, JsValue>();
    }

    internal bool WeakMapHas(JsValue key)
    {
        return _table.TryGetValue(key, out _);
    }

    internal bool WeakMapDelete(JsValue key)
    {
        return _table.Remove(key);
    }

    internal void WeakMapSet(JsValue key, JsValue value)
    {
        if (!key.CanBeHeldWeakly(_engine.GlobalSymbolRegistry))
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "WeakMap key must be an object, got " + key);
        }

#if SUPPORTS_WEAK_TABLE_ADD_OR_UPDATE
         _table.AddOrUpdate(key, value);
#else
        _table.Remove(key);
        _table.Add(key, value);
#endif
    }

    internal JsValue WeakMapGet(JsValue key)
    {
        if (!_table.TryGetValue(key, out var value))
        {
            return Undefined;
        }

        return value;
    }

}
