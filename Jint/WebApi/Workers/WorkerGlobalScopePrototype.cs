#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Workers;

/// <summary>
/// <c>WorkerGlobalScope.prototype</c> — the interface prototype object a worker's global object inherits from,
/// one link above <c>DedicatedWorkerGlobalScope.prototype</c>.
/// <para>
/// https://html.spec.whatwg.org/multipage/workers.html#the-workerglobalscope-common-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// It carries <c>constructor</c> and the interface's <c>@@toStringTag</c> and nothing else, and that is a
/// documented divergence rather than an omission. HTML puts <c>self</c>, <c>location</c>, <c>navigator</c>,
/// <c>importScripts()</c> and the event-handler attributes here; in Jint every one of those is <b>per
/// connection</b> — <c>importScripts</c> and the handler attributes close over the <c>WorkerLink</c> this
/// worker was made with — while an interface prototype object is a realm intrinsic, built once and shared. So
/// <see cref="WorkerGlobalScope.Install"/> keeps them as own properties of the global object, exactly where
/// they were before this pair existed, and what the pair adds is the <b>brand</b>: the thing
/// <c>'DedicatedWorkerGlobalScope' in self</c> and <c>self instanceof WorkerGlobalScope</c> ask about, which
/// is what every worker feature-detect in the wild is written against.
/// </para>
/// <para>
/// The <c>[[Prototype]]</c> is <c>%Object.prototype%</c> where HTML has <c>EventTarget.prototype</c>; see
/// <see cref="WorkerGlobalScopeConstructor"/> for why that link is deliberately not claimed.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class WorkerGlobalScopePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly WorkerGlobalScopeConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString WorkerGlobalScopeToStringTag = new("WorkerGlobalScope");

    internal WorkerGlobalScopePrototype(
        Engine engine,
        Realm realm,
        WorkerGlobalScopeConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }
}
#endif
