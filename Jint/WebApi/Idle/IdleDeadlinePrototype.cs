#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Idle;

/// <summary>
/// <c>IdleDeadline.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/requestidlecallback/#idledeadline
/// </para>
/// </summary>
/// <remarks>
/// Both members brand-check their receiver, so <c>IdleDeadline.prototype.timeRemaining()</c> and a plain
/// object borrowed into the receiver position raise a <c>TypeError</c> rather than answering something
/// meaningless. The same documented simplification the other web-API prototypes carry applies here: the
/// operation is non-enumerable where WebIDL makes an interface prototype object's operations enumerable, while
/// the attribute is enumerable as it should be.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class IdleDeadlinePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IdleDeadlineConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString IdleDeadlineToStringTag = new("IdleDeadline");

    internal IdleDeadlinePrototype(
        Engine engine,
        Realm realm,
        IdleDeadlineConstructor constructor,
        ObjectInstance objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://w3c.github.io/requestidlecallback/#dom-idledeadline-timeremaining
    /// </summary>
    [JsFunction(Name = "timeRemaining", Length = 0)]
    private JsNumber TimeRemaining(JsValue thisObject) => JsNumber.Create(Brand(thisObject).TimeRemaining());

    /// <summary>
    /// https://w3c.github.io/requestidlecallback/#dom-idledeadline-didtimeout
    /// </summary>
    [JsAccessor("didTimeout", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean DidTimeoutGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).DidTimeout);

    private JsIdleDeadline Brand(JsValue thisObject)
    {
        if (thisObject is JsIdleDeadline deadline)
        {
            return deadline;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not an IdleDeadline");
        return null!;
    }
}
#endif
