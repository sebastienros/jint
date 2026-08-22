#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Workers;

/// <summary>
/// The <c>WorkerGlobalScope</c> interface object, and the base of the pair a worker's global object is an
/// instance of.
/// <para>
/// https://html.spec.whatwg.org/multipage/workers.html#the-workerglobalscope-common-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The interface declares no constructor operation, so the interface object exists and is a function but
/// refuses to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. It is
/// <c>[Exposed=Worker]</c>, so it is installed on a <b>worker</b> engine's global object alone and never on the
/// engine that created the worker; <see cref="WorkerGlobalScope.Install"/> is the one caller.
/// </para>
/// <para>
/// <b>The prototype chain stops at <c>%Object.prototype%</c>, where HTML has <c>EventTarget</c>.</b> The
/// specification declares <c>interface WorkerGlobalScope : EventTarget</c>, and Jint's global object is not an
/// <c>EventTarget</c> — <c>addEventListener</c> is an ordinary function on the global, bound to the synthetic
/// listener list <c>GlobalEventTarget</c> keeps, which is the "suitable alternative mechanism available at the
/// global scope" WinterTC's Minimum Common API §6 blesses. Claiming the inheritance would make
/// <c>self instanceof EventTarget</c> true while <c>EventTarget.prototype.addEventListener.call(self)</c>
/// failed its brand check, which is precisely the kind of half-truth this exposure decision exists to remove.
/// What <i>is</i> claimed is claimed for real: the worker global's <c>[[Prototype]]</c> genuinely is
/// <c>DedicatedWorkerGlobalScope.prototype</c>, whose <c>[[Prototype]]</c> genuinely is the object this
/// interface object's <c>prototype</c> names, so <c>self instanceof WorkerGlobalScope</c> is answered by
/// walking the chain and by nothing else.
/// </para>
/// </remarks>
internal sealed class WorkerGlobalScopeConstructor : Constructor
{
    private static readonly JsString _functionName = new("WorkerGlobalScope");

    internal WorkerGlobalScopeConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new WorkerGlobalScopePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal WorkerGlobalScopePrototype PrototypeObject { get; }

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
