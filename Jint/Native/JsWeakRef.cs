using Jint.Native.Object;

namespace Jint.Native;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-weak-ref-instances
/// </summary>
internal sealed class JsWeakRef : ObjectInstance
{
    private readonly WeakReference<JsValue> _weakRefTarget;

    public JsWeakRef(Engine engine, JsValue target) : base(engine)
    {
        _weakRefTarget = new WeakReference<JsValue>(target);
    }

    public JsValue WeakRefDeref()
    {
        if (_weakRefTarget.TryGetTarget(out var target))
        {
            _engine.AddToKeptObjects(target);
            return target;
        }

        return Undefined;
    }
}
