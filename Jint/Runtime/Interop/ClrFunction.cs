using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop;

/// <summary>
/// Wraps a CLR method into a JS function.
/// </summary>
public sealed class ClrFunction : Function, IEquatable<ClrFunction>
{
    internal readonly JsCallDelegate _func;
    private readonly bool _bubbleExceptions;

    public ClrFunction(
        Engine engine,
        string name,
        JsCallDelegate func,
        int length = 0,
        PropertyFlag lengthFlags = PropertyFlag.AllForbidden)
        : base(engine, engine.Realm, JsString.CachedCreate(name))
    {
        _func = func;

        _prototype = engine._originalIntrinsics.Function.PrototypeObject;

        _length = lengthFlags == PropertyFlag.AllForbidden
            ? PropertyDescriptor.AllForbiddenDescriptor.ForNumber(length)
            : new PropertyDescriptor(JsNumber.Create(length), lengthFlags);

        _bubbleExceptions = ClrExceptionsBubble(engine);
    }

    /// <summary>
    /// Creates a function pinned to <paramref name="realm"/> rather than to whichever realm happens to be
    /// active. The public constructor above reads <c>engine.Realm</c> at call time and anchors the function
    /// to the engine's original intrinsics, which is right for a host wiring a function up front but wrong
    /// for one created lazily: a shared-shape prototype can materialize a member while another realm is
    /// running (from a ShadowRealm callback, say), and the function must still belong to the realm that owns
    /// the object it came from.
    /// </summary>
    internal ClrFunction(
        Engine engine,
        Realm realm,
        string name,
        JsCallDelegate func,
        int length,
        PropertyFlag lengthFlags = PropertyFlag.AllForbidden)
        : base(engine, realm, JsString.CachedCreate(name))
    {
        _func = func;

        _prototype = realm.Intrinsics.Function.PrototypeObject;

        _length = lengthFlags == PropertyFlag.AllForbidden
            ? PropertyDescriptor.AllForbiddenDescriptor.ForNumber(length)
            : new PropertyDescriptor(JsNumber.Create(length), lengthFlags);

        _bubbleExceptions = ClrExceptionsBubble(engine);
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments) => _bubbleExceptions ? _func(thisObject, arguments) : CallSlow(thisObject, arguments);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private JsValue CallSlow(JsValue thisObject, JsCallArguments arguments)
    {
        try
        {
            return _func(thisObject, arguments);
        }
        catch (Exception e) when (e is not JavaScriptException && !Throw.MustPropagateHostException(e))
        {
            return TranslateClrException(_engine, e);
        }
    }

    public override bool Equals(JsValue? other) => Equals(other as ClrFunction);

    public override bool Equals(object? obj) => Equals(obj as ClrFunction);

    public bool Equals(ClrFunction? other)
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

    public override int GetHashCode() => _func.GetHashCode();
}
