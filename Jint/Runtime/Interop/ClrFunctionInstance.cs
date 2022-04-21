using System;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop;

/// <summary>
/// Signature for CLR host methods that can be called as JavaScript functions.
/// </summary>
public delegate JsValue ClrCallable(JsValue thisObject, in Arguments arguments);

/// <summary>
/// Wraps a Clr method into a FunctionInstance
/// </summary>
public sealed class ClrFunctionInstance : FunctionInstance, IEquatable<ClrFunctionInstance>
{
    private readonly string _name;
    internal readonly ClrCallable _func;

    public ClrFunctionInstance(
        Engine engine,
        string name,
        ClrCallable func,
        int length = 0,
        PropertyFlag lengthFlags = PropertyFlag.AllForbidden)
        : base(engine, engine.Realm, name != null ? new JsString(name) : null)
    {
        _name = name;
        _func = func;

        _prototype = engine._originalIntrinsics.Function.PrototypeObject;

        _length = lengthFlags == PropertyFlag.AllForbidden
            ? PropertyDescriptor.AllForbiddenDescriptor.ForNumber(length)
            : new PropertyDescriptor(JsNumber.Create(length), lengthFlags);
    }

    public override JsValue Call(JsValue thisObject, in Arguments arguments) => _func(thisObject, arguments);

    public override bool Equals(JsValue obj)
    {
        return Equals(obj as ClrFunctionInstance);
    }

    public bool Equals(ClrFunctionInstance other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (_func == other._func)
        {
            return true;
        }

        return false;
    }

    public override string ToString() => "function " + _name + "() { [native code] }";
}
