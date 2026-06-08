using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Environments;

namespace Jint.Runtime;

public sealed class Realm
{
    internal readonly Dictionary<Node, JsArray> _templateMap = new();

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