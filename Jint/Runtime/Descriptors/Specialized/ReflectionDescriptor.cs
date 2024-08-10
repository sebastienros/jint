using System.Reflection;
using Jint.Native;
using Jint.Runtime.Interop;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Descriptors.Specialized;

internal sealed class ReflectionDescriptor : PropertyDescriptor
{
    private readonly Engine _engine;
    private readonly ReflectionAccessor _reflectionAccessor;
    private readonly object _target;
    private readonly string _propertyName;

    private JsValue? _get;
    private JsValue? _set;

    public ReflectionDescriptor(
        Engine engine,
        ReflectionAccessor reflectionAccessor,
        object target,
        string propertyName,
        bool enumerable)
        : base((enumerable ? PropertyFlag.Enumerable : PropertyFlag.None) | PropertyFlag.CustomJsValue)
    {
        _flags |= PropertyFlag.NonData;
        _engine = engine;
        _reflectionAccessor = reflectionAccessor;
        _target = target;
        _propertyName = propertyName;
    }

    public override JsValue? Get
    {
        get
        {
            if (_reflectionAccessor.Readable)
            {
                return _get ??= new GetterFunction(_engine, DoGet);
            }

            return null;
        }
    }

    public override JsValue? Set
    {
        get
        {
            if (_reflectionAccessor.Writable && _engine.Options.Interop.AllowWrite)
            {
                return _set ??= new SetterFunction(_engine, DoSet);
            }

            return null;
        }
    }

    protected internal override JsValue? CustomValue
    {
        get => DoGet(thisObj: null);
        set => DoSet(thisObj: null, value);
    }

    private JsValue DoGet(JsValue? thisObj)
    {
        var value = _reflectionAccessor.GetValue(_engine, _target, _propertyName);
        var type = _reflectionAccessor.MemberType ?? value?.GetType();
        return JsValue.FromObjectWithType(_engine, value, type);
    }

    private void DoSet(JsValue? thisObj, JsValue? v)
    {
        try
        {
            _reflectionAccessor.SetValue(_engine, _target, _propertyName, v!);
        }
        catch (TargetInvocationException exception)
        {
            ExceptionHelper.ThrowMeaningfulException(_engine, exception);
        }
    }
}
