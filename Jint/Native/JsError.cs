using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.CallStack;
using Jint.Runtime.Descriptors;

namespace Jint.Native;

public sealed class JsError : ErrorInstance
{
    /// <summary>
    /// The rendered implementation-defined stack trace string, materialized on first read from
    /// <see cref="_stackCapture"/> (or set directly by a materializing path). Returned by the
    /// <c>get Error.prototype.stack</c> accessor (the error-stack-accessor proposal).
    /// </summary>
    internal JsValue? _stack;

    /// <summary>
    /// A deferred snapshot of the call stack captured at construction; rendered into <see cref="_stack"/>
    /// on the first <c>error.stack</c> read. Non-null until rendered (then cleared to release the snapshot).
    /// </summary>
    internal ErrorStackCapture? _stackCapture;

    /// <summary>
    /// Virtual storage for the <c>message</c> own property. When an error is constructed with a message
    /// argument, that message is a writable/non-enumerable/configurable own data property — but the vast
    /// majority of errors carry only this one own property, so materializing the full dictionary machinery
    /// (PropertyDictionary + node array + PropertyDescriptor) per error is pure allocation overhead.
    /// <para/>
    /// While this field is non-null the message is served from here (see <see cref="Get"/> /
    /// <see cref="GetOwnProperty"/> / <see cref="Set"/>) with no property storage. Any operation that needs
    /// a real own property — <see cref="DefineOwnProperty"/> (redefine, freeze, or adding another own
    /// property such as <c>cause</c>/<c>errors</c>), or enumeration with other keys present — deopts via
    /// <see cref="MaterializeMessage"/>, after which the error behaves like an ordinary dictionary-backed
    /// object. A C#-<c>null</c> value means "no virtual message" (never had one, or already materialized).
    /// </summary>
    private JsValue? _message;

    internal JsError(Engine engine) : base(engine, ObjectClass.Error)
    {
        // The message own property is served virtually, so route member reads through this exotic [[Get]]
        // instead of the ordinary GetOwnProperty-first inline cache (which could cache a stale synthetic
        // message descriptor). Errors gain nothing from the cache anyway — their identity churns per throw.
        _type |= InternalTypes.ExoticGet;
    }

    /// <summary>
    /// Installs the construction-time <c>message</c> as a virtual own property (writable, non-enumerable,
    /// configurable) without allocating any property storage.
    /// </summary>
    internal void SetVirtualMessage(JsValue message)
    {
        _message = message;
    }

    /// <summary>
    /// Forces the virtual <c>message</c> into real storage. Callers that add another own property through a
    /// raw store (bypassing <see cref="DefineOwnProperty"/>) — notably the host-side <c>stack</c> property
    /// installed by <see cref="Jint.Runtime.JavaScriptException"/> — must call this first so the message
    /// keeps its construction-time position as the earliest own key.
    /// </summary>
    internal void EnsureMessageMaterialized() => MaterializeMessage();

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        // Fast path for the hot `error.message` read: serve from the field, no descriptor allocation.
        if (_message is not null && ReferenceEquals(this, receiver) && CommonProperties.Message.Equals(property))
        {
            return _message;
        }

        return base.Get(property, receiver);
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (_message is not null && CommonProperties.Message.Equals(property))
        {
            // Synthesize the descriptor on demand (rare vs. plain reads): { value, writable, !enumerable, configurable }.
            return new PropertyDescriptor(_message, writable: true, enumerable: false, configurable: true);
        }

        return base.GetOwnProperty(property);
    }

    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        // The virtual message is a writable data property: an in-place write updates the field.
        if (_message is not null && ReferenceEquals(this, receiver) && CommonProperties.Message.Equals(property))
        {
            _message = value;
            return true;
        }

        return base.Set(property, value, receiver);
    }

    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        // Any define (redefine of message, freeze/seal, or adding another own property such as cause/errors)
        // needs real property storage — and materializing first keeps message as the earliest own key.
        MaterializeMessage();
        return base.DefineOwnProperty(property, desc);
    }

    public override bool Delete(JsValue property)
    {
        // `delete error.message` — the property is configurable, so drop the virtual field.
        if (_message is not null && CommonProperties.Message.Equals(property))
        {
            _message = null;
            return true;
        }

        return base.Delete(property);
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        if (_message is not null)
        {
            // Invariant: while the message is virtual no other own property exists, because every addition
            // routes through the materializing DefineOwnProperty. Defensively fall back to the base ordering
            // if that invariant was somehow bypassed (e.g. a raw internal store).
            if (_properties is { Count: > 0 } || _symbols is { Count: > 0 })
            {
                MaterializeMessage();
            }
            else
            {
                var keys = base.GetOwnPropertyKeys(types);
                if ((types & Types.String) != Types.Empty)
                {
                    keys.Insert(0, CommonProperties.Message);
                }
                return keys;
            }
        }

        return base.GetOwnPropertyKeys(types);
    }

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        // Rare path; materialize for correct insertion ordering and descriptor exposure, then defer to base.
        MaterializeMessage();
        return base.GetOwnProperties();
    }

    /// <summary>
    /// Deopts the virtual <c>message</c> into ordinary dictionary storage, reproducing the exact
    /// construction-time descriptor. No-op once the message is already materialized or was never set.
    /// </summary>
    private void MaterializeMessage()
    {
        var message = _message;
        if (message is null)
        {
            return;
        }

        // Clear before storing so any re-entrant read observes a single, consistent source.
        _message = null;
        SetProperty(CommonProperties.Message, new PropertyDescriptor(message, writable: true, enumerable: false, configurable: true));
    }
}
