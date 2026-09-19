using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Environments;

namespace Jint.Runtime;

public sealed class Realm
{
    /// <summary>
    /// The realm's [[TemplateMap]]: the template object canonicalized for each tagged-template Parse Node
    /// (https://tc39.es/ecma262/#sec-gettemplateobject). Weak on both ends, and it has to be.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Weak key.</b> A strong <c>Dictionary&lt;Node, JsArray&gt;</c> held the parse node, so a host
    /// evaluating a fresh source per operation on one long-lived engine - the <c>Evaluate(string)</c> shape,
    /// which parses a new AST every call - grew it forever at ~1.35 KB a call (issue #4117). The ceiling that
    /// bounds the handler-tree caches is no remedy here: the spec requires one template object per Parse Node
    /// per realm, so evicting a live site is script-visible. A dead parse node, on the other hand, can never
    /// be asked again, which makes key weakness exactly the right lifetime.
    /// </para>
    /// <para>
    /// <b>Weak value, and not merely for size.</b> A <see cref="ConditionalWeakTable{TKey, TValue}"/> keeps
    /// its value alive on KEY reachability, so holding the <see cref="JsArray"/> directly would have an AST
    /// rooted by a shared <c>Prepared&lt;Script&gt;</c> pin the array, and through it the engine and this
    /// realm - the dead-engine retention <c>GarbageCollectionTests.SharedPreparedScriptDoesNotRetainEngines</c>
    /// exists to catch, and the same hazard written up beside <c>Engine._functionDefinitions</c>. The
    /// <see cref="WeakReference{T}"/> holds nothing engine-affine, so an entry costs a handle until its node
    /// dies and nothing after that.
    /// </para>
    /// <para>
    /// Losing a collected template object is not observable: identity can only be compared by a script that
    /// still holds the object, and a script that still holds it has kept the weak reference alive. The
    /// per-site identity test262 pins (<c>cache-same-site</c>, <c>template-object-template-map</c>,
    /// <c>cache-eval-inner-function</c>) is exactly that case.
    /// </para>
    /// </remarks>
    internal ConditionalWeakTable<Node, WeakReference<JsArray>>? _templateMap;

    /// <summary>
    /// Compilation cache for repeated new Function(...) with identical sources,
    /// see <see cref="Native.Function.Function"/> CreateDynamicFunction.
    /// </summary>
    internal Dictionary<Native.Function.DynamicFunctionCacheKey, Native.Function.DynamicFunctionCacheEntry>? _dynamicFunctionCache;

    /// <summary>
    /// Two-touch promotion slot for <see cref="_dynamicFunctionCache"/>: a source is cached
    /// only when seen twice, keeping one-shot compilations insert-free.
    /// </summary>
    internal Native.Function.DynamicFunctionCacheKey _dynamicFunctionProbationKey;


    // helps when debugging which nested realm we are in...
#if DEBUG
    private static int globalId = 1;

    public Realm()
    {
        Id = globalId++;
    }

    internal int Id;
#endif

    /// <summary>
    /// The intrinsic values used by code associated with this realm.
    /// </summary>
    public Intrinsics Intrinsics { get; internal set; } = null!;

    /// <summary>
    /// The global object for this realm.
    /// </summary>
    public ObjectInstance GlobalObject { get; internal set; } = null!;

    /// <summary>
    /// The global environment for this realm.
    /// </summary>
    internal GlobalEnvironment GlobalEnv { get; set; } = null!;

    /// <summary>
    /// Field reserved for use by hosts that need to associate additional information with a Realm Record.
    /// </summary>
    public object? HostDefined { get; set; }
}