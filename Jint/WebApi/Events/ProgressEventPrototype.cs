#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Events;

/// <summary>
/// <c>ProgressEvent.prototype</c> — the interface prototype object.
/// <para>
/// https://xhr.spec.whatwg.org/#interface-progressevent
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>Event.prototype</c>, so a progress event carries every <c>Event</c> member
/// and <c>new ProgressEvent('x') instanceof Event</c> holds.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class ProgressEventPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ProgressEventConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ProgressEventToStringTag = new("ProgressEvent");

    internal ProgressEventPrototype(
        Engine engine,
        Realm realm,
        ProgressEventConstructor constructor,
        ObjectInstance eventPrototype) : base(engine, realm)
    {
        _prototype = eventPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-progressevent-lengthcomputable
    /// </summary>
    [JsAccessor("lengthComputable", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean LengthComputableGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).LengthComputable);

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-progressevent-loaded
    /// </summary>
    [JsAccessor("loaded", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber LoadedGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).Loaded);

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-progressevent-total
    /// </summary>
    [JsAccessor("total", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber TotalGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).Total);

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a <c>ProgressEvent</c> raises a
    /// <c>TypeError</c>.
    /// </summary>
    private JsProgressEvent Brand(JsValue thisObject)
    {
        if (thisObject is JsProgressEvent progressEvent)
        {
            return progressEvent;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a ProgressEvent");
        return null!;
    }
}
#endif
