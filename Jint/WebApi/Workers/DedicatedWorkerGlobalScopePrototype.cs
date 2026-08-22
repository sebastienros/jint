#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Workers;

/// <summary>
/// <c>DedicatedWorkerGlobalScope.prototype</c> — the interface prototype object a worker's global object
/// directly inherits from.
/// <para>
/// https://html.spec.whatwg.org/multipage/workers.html#dedicated-workers-and-the-dedicatedworkerglobalscope-interface
/// </para>
/// </summary>
/// <remarks>
/// Like <see cref="WorkerGlobalScopePrototype"/> it carries <c>constructor</c> and the interface's
/// <c>@@toStringTag</c> and nothing else: <c>name</c>, <c>postMessage()</c>, <c>close()</c>, <c>onmessage</c>
/// and <c>onmessageerror</c> are per-connection state and stay own properties of the global object, for the
/// reason that file records. The <c>@@toStringTag</c> is what makes
/// <c>Object.prototype.toString.call(self)</c> answer <c>[object DedicatedWorkerGlobalScope]</c>, as it does
/// in a browser.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class DedicatedWorkerGlobalScopePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly DedicatedWorkerGlobalScopeConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString DedicatedWorkerGlobalScopeToStringTag = new("DedicatedWorkerGlobalScope");

    internal DedicatedWorkerGlobalScopePrototype(
        Engine engine,
        Realm realm,
        DedicatedWorkerGlobalScopeConstructor constructor,
        WorkerGlobalScopePrototype basePrototype) : base(engine, realm)
    {
        _prototype = basePrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }
}
#endif
