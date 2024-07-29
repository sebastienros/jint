using Jint.Native;
using Jint.Runtime;

namespace Jint.Pooling;

/// <summary>
/// Cache reusable <see cref="Reference" /> instances as we allocate them a lot.
/// </summary>
internal sealed class ReferencePool
{
    private const int PoolSize = 10;
    private readonly ObjectPool<Reference> _pool;

    public ReferencePool()
    {
        _pool = new ObjectPool<Reference>(Factory, PoolSize);
    }

    private static Reference Factory()
    {
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
