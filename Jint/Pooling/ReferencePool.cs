using Jint.Native;
using Jint.Runtime.References;

namespace Jint
{
    /// <summary>
    /// Cache reusable <see cref="Reference" /> instances as we allocate them a lot.
    /// </summary>
    internal sealed class ReferencePool
    {
        private const int PoolSize = 10;
        private readonly Reference[] _pool;
        private int _currentSize;

        public ReferencePool()
        {
            // pre-allocate so we don't show up in benchmarks
            _pool = new Reference[PoolSize];
            for (var i = 0; i < PoolSize; ++i)
            {
                _pool[i] = new Reference(JsValue.Undefined, string.Empty, false);
            }

            _currentSize = PoolSize;
        }

        public Reference Rent(JsValue baseValue, string name, bool strict)
        {
            if (_currentSize > 0)
            {
                _currentSize--;
                var reference = _pool[_currentSize];
                return reference.Reassign(baseValue, name, strict);
            }

            return new Reference(baseValue, name, strict);
        }

        public void Return(Reference reference)
        {
            if (reference == null || _currentSize >= PoolSize)
            {
                return;
            }
            _pool[_currentSize] = reference;
            _currentSize++;
        }
    }
}