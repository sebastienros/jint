using Jint.Native;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Descriptors.Specialized;

/// <summary>
/// Property descriptor emitted by [JsAccessible]. Calls into the generated accessor's typed
/// <see cref="GeneratedReflectionAccessor.GetJsValue"/> / <see cref="GeneratedReflectionAccessor.SetFromJsValue"/>
/// methods directly, skipping <see cref="JsValue.FromObjectWithType"/> and the
/// <see cref="System.Reflection"/> round-trip that <see cref="ReflectionDescriptor"/> incurs.
/// </summary>
internal sealed class GeneratedReflectionDescriptor : PropertyDescriptor
{
    private readonly Engine _engine;
    private readonly GeneratedReflectionAccessor _accessor;
    private readonly object _target;

    private JsValue? _get;
    private JsValue? _set;

    public GeneratedReflectionDescriptor(
        Engine engine,
        GeneratedReflectionAccessor accessor,
        object target,
        bool enumerable)
        : base((enumerable ? PropertyFlag.Enumerable : PropertyFlag.None) | PropertyFlag.CustomJsValue)
    {
        _flags |= PropertyFlag.NonData;
        _engine = engine;
        _accessor = accessor;
        _target = target;
    }

    public override JsValue? Get
    {
        get
        {
            if (_accessor.Readable)
            {
                return _get ??= new Jint.Runtime.Interop.GetterFunction(_engine, DoGet);
            }

            return null;
        }
    }

    public override JsValue? Set
    {
        get
        {
            if (_accessor.Writable && _engine.Options.Interop.AllowWrite)
            {
                return _set ??= new Jint.Runtime.Interop.SetterFunction(_engine, DoSet);
            }

            return null;
        }
    }

    protected internal override JsValue? CustomValue
    {
        get => DoGet(thisObj: null);
        set => DoSet(thisObj: null, value);
    }

    private JsValue DoGet(JsValue? thisObj) => _accessor.GetJsValue(_engine, _target);

    private void DoSet(JsValue? thisObj, JsValue? v) => _accessor.SetFromJsValue(_engine, _target, v!);
}
