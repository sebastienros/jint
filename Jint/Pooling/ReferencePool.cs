using Jint.Native;
using Jint.Runtime;

namespace Jint.Pooling;

/// <summary>
/// Cache reusable <see cref="Reference" /> instances as we allocate them a lot.
/// </summary>
internal sealed class ReferencePool
{
    /// <summary>
    /// How many instances the pool can hold, and therefore the upper bound on how many distinct
    /// <see cref="Reference" /> instances a balanced rent/return workload can ever be handed.
    /// </summary>
    internal const int PoolSize = 10;
    private readonly ObjectPool<Reference> _pool;

    public ReferencePool()
    {
        _pool = new ObjectPool<Reference>(Factory, PoolSize);
    }

    /// <summary>
    /// How many instances the pool has had to create because it had none to hand out. A workload that
    /// rents and returns in balance settles at the pool's capacity; a path that rents without returning
    /// keeps this climbing, one per rent, which is what makes it worth asserting on in tests.
    /// </summary>
    internal int CreatedCount { get; private set; }

    /// <summary>
    /// How many instances the pool is holding right now, for
    /// <see cref="Engine.AdvancedOperations.GetMemoryReport(int)"/>; at most <see cref="PoolSize"/>.
    /// </summary>
    internal int PooledCount => _pool.PooledCount;

    private Reference Factory()
    {
        CreatedCount++;
        return new Reference(JsValue.Undefined, JsString.Empty, false, null);
    }

    public Reference Rent(JsValue baseValue, JsValue name, bool strict, JsValue? thisValue)
    {
        return _pool.Allocate().Reassign(baseValue, name, strict, thisValue);
    }

    public void Return(Reference? reference)
    {
        if (reference == null)
        {
            return;
        }
        _pool.Free(reference);
    }
}
