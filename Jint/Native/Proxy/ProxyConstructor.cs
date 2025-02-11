#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of constructor methods return JsValue

using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Proxy;

/// <summary>
/// https://tc39.es/ecma262/#sec-proxy-constructor
/// </summary>
internal sealed class ProxyConstructor : Constructor
{
    private static readonly JsString _name = new JsString("Proxy");
    private static readonly JsString PropertyProxy = new JsString("proxy");
    private static readonly JsString PropertyRevoke = new JsString("revoke");

    internal ProxyConstructor(
        Engine engine,
        Realm realm)
        : base(engine, realm, _name)
    {
        _length = new PropertyDescriptor(2, PropertyFlag.Configurable);
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            ["revocable"] = new PropertyDescriptor(new ClrFunction(_engine, "revocable", Revocable, 2, PropertyFlag.Configurable), true, true, true)
        };
        SetProperties(properties);
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        return Construct(arguments.At(0), arguments.At(1));
    }

    protected internal override ObjectInstance? GetPrototypeOf()
    {
        return _realm.Intrinsics.Function.Prototype;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-target-handler
    /// </summary>
    public JsProxy Construct(JsValue target, JsValue handler)
    {
        return ProxyCreate(target, handler);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy.revocable
    /// </summary>
    private JsValue Revocable(JsValue thisObject, JsCallArguments arguments)
    {
        var p = ProxyCreate(arguments.At(0), arguments.At(1));

        JsValue Revoke(JsValue thisObject, JsCallArguments arguments)
        {
            p._handler = null;
            p._target = null!;
            return Undefined;
        }

        var result = _realm.Intrinsics.Object.Construct([]);
        result.DefineOwnProperty(PropertyRevoke, new PropertyDescriptor(new ClrFunction(_engine, name: "", Revoke, 0, PropertyFlag.Configurable), PropertyFlag.ConfigurableEnumerableWritable));
        result.DefineOwnProperty(PropertyProxy, new PropertyDescriptor(p, PropertyFlag.ConfigurableEnumerableWritable));
        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxycreate
    /// </summary>
    private JsProxy ProxyCreate(JsValue target, JsValue handler)
    {
        if (target is not ObjectInstance targetObject)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Cannot create proxy with a non-object as target");
            return null;
        }

        if (handler is not ObjectInstance targetHandler)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Cannot create proxy with a non-object as handler");
            return null;
        }

        var p = new JsProxy(Engine, targetObject, targetHandler);
        return p;
    }
}
