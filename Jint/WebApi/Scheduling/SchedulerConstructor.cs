#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Scheduling;

/// <summary>
/// The <c>Scheduler</c> interface object.
/// <para>
/// https://wicg.github.io/scheduling-apis/#sec-scheduler
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The IDL is <c>[Exposed=(Window, Worker)] interface Scheduler { Promise&lt;any&gt; postTask(...);
/// Promise&lt;undefined&gt; yield(); };</c> — <b>no constructor operation</b>, so the interface object exists
/// and is a function but refuses to construct anything,
/// https://webidl.spec.whatwg.org/#es-interface-call. Its two siblings in the same specification do declare
/// one (<c>TaskController</c> and <c>TaskPriorityChangeEvent</c>), which is why they are constructible here
/// and this is not.
/// </para>
/// <para>
/// <b>Why it is a global, given the interface is a WICG draft rather than a living standard.</b> The draft
/// status was already priced in when the feature was taken: <c>WebApiFeatures.Scheduler</c> installs
/// <c>scheduler</c>, <c>TaskController</c>, <c>TaskSignal</c> and <c>TaskPriorityChangeEvent</c>, three of
/// which are interface objects. Withholding the fourth would make this API the only one whose singleton
/// cannot answer <c>scheduler instanceof Scheduler</c> — the feature detection a library that chunks work
/// opens with — while its own controller and signal can. The interface object also has to exist whether or
/// not it is named: <c>Scheduler.prototype</c> is where the two operations live, and its <c>constructor</c>
/// property is that object. So the only question a global settles is whether a script can reach it by name
/// instead of through <c>Object.getPrototypeOf(scheduler).constructor</c>, and Chrome, which is the only
/// implementation there is, exposes it.
/// </para>
/// </remarks>
internal sealed class SchedulerConstructor : Constructor
{
    private static readonly JsString _functionName = new("Scheduler");

    internal SchedulerConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new SchedulerPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal SchedulerPrototype PrototypeObject { get; }

    /// <summary>
    /// An interface without a constructor operation is not constructible.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}
#endif
