using Jint.Native;
using Jint.Runtime.References;

namespace Jint.Pooling
{
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
            return new Reference(JsValue.Undefined, string.Empty, false);
        }

        public Reference Rent(JsValue baseValue, string name, bool strict)
        {
            return _pool.Allocate().Reassign(baseValue, name, strict);
        }

        public void Return(Reference reference)
        {
            if (reference == null)
            {
                return;
            }
            _pool.Free(reference);;
        }
    }
}