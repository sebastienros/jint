#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Workers;

/// <summary>
/// The <c>DedicatedWorkerGlobalScope</c> interface object — what a worker's own global object is an instance
/// of.
/// <para>
/// https://html.spec.whatwg.org/multipage/workers.html#dedicated-workers-and-the-dedicatedworkerglobalscope-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// It inherits from <c>WorkerGlobalScope</c>, so its <c>[[Prototype]]</c> is that interface object rather than
/// <c>%Function.prototype%</c> — https://webidl.spec.whatwg.org/#interface-object — and its <c>prototype</c>'s
/// <c>[[Prototype]]</c> is <c>WorkerGlobalScope.prototype</c>. The interface declares no constructor
/// operation, so it refuses <c>new</c>.
/// </para>
/// <para>
/// It is <c>[Exposed=DedicatedWorker]</c>, so it exists on a worker engine's global and on no other. That is
/// what the canonical worker feature-detect asks —
/// <c>'DedicatedWorkerGlobalScope' in self &amp;&amp; self instanceof DedicatedWorkerGlobalScope</c> — and
/// answering the second half honestly is the whole reason this pair exists: web-platform-tests'
/// <c>workers/modules/</c> fixtures register their <c>onmessage</c> handler inside exactly that branch, so a
/// runtime without the interface object does not fail an assertion, it never answers at all.
/// </para>
/// </remarks>
internal sealed class DedicatedWorkerGlobalScopeConstructor : Constructor
{
    private static readonly JsString _functionName = new("DedicatedWorkerGlobalScope");

    internal DedicatedWorkerGlobalScopeConstructor(Engine engine, Realm realm, WorkerGlobalScopeConstructor baseConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = baseConstructor;
        PrototypeObject = new DedicatedWorkerGlobalScopePrototype(engine, realm, this, baseConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal DedicatedWorkerGlobalScopePrototype PrototypeObject { get; }

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
