using System;
using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Pooling
{
    /// <summary>
    /// Cache reusable <see cref="JsValue" /> array instances as we allocate them a lot.
    /// </summary>
    internal sealed class JsValueArrayPool
    {
        private const int PoolSize = 15;
        private const int MaxPooledArraySize = 128;
        private readonly ObjectPool<JsValue[]> _pool;

        public JsValueArrayPool()
        {
            _pool = new ObjectPool<JsValue[]>(Factory, PoolSize);
        }

        private static JsValue[] Factory()
        {
            return new JsValue[MaxPooledArraySize];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayRentHolder RentArray(int size)
        {
            var array = size switch
            {
                0 => Array.Empty<JsValue>(),
                <= MaxPooledArraySize => _pool.Allocate(),
                _ => new JsValue[size]
            };

            return new ArrayRentHolder(_pool, array, size);
        }

        internal readonly struct ArrayRentHolder : IDisposable
        {
            private readonly ObjectPool<JsValue[]> _pool;
            internal readonly JsValue[] _array;
            private readonly int _size;

            public ArrayRentHolder(ObjectPool<JsValue[]> pool, JsValue[] array, int size)
            {
                _pool = pool;
                _array = array;
                _size = size;
            }

            public JsValue this[int i]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set { _array[i] = value; }
            }

            public Span<JsValue> Span => _array.AsSpan(0, _size);

            public void Dispose()
            {
                if (_array.Length > 0)
                {
                    _pool.Free(_array);
                }
            }
        }
    }
}
