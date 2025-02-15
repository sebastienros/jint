using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
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

        _bubbleExceptions = _engine.Options.Interop.ExceptionHandler == Options.InteropOptions._defaultExceptionHandler;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments) => _bubbleExceptions ? _func(thisObject, arguments) : CallSlow(thisObject, arguments);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private JsValue CallSlow(JsValue thisObject, JsCallArguments arguments)
    {
        try
        {
            return _func(thisObject, arguments);
        }
        catch (Exception e) when (e is not JavaScriptException)
        {
            if (_engine.Options.Interop.ExceptionHandler(e))
            {
                ExceptionHelper.ThrowJavaScriptException(_realm.Intrinsics.Error, e.Message);
            }
            else
            {
                ExceptionDispatchInfo.Capture(e).Throw();
            }

            return Undefined;
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
