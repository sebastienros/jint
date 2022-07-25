using Jint.Native.Object;

namespace Jint.Native.WeakRef;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-weak-ref-instances
/// </summary>
internal sealed class WeakRefInstance : ObjectInstance
{
    private readonly WeakReference<ObjectInstance> _weakRefTarget;

    public WeakRefInstance(Engine engine, ObjectInstance target) : base(engine)
    {
        _weakRefTarget = new WeakReference<ObjectInstance>(target);
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
