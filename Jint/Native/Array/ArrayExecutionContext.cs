using System.Collections.Generic;
using System.Threading;

namespace Jint.Native.Array
{
    /// <summary>
    /// Helper to cache common data structures needed in array access on a per thread basis.
    /// </summary>
    internal class ArrayExecutionContext
    {
        // cache key container for array iteration for less allocations
        private static readonly ThreadLocal<ArrayExecutionContext> _executionContext = new ThreadLocal<ArrayExecutionContext>(() => new ArrayExecutionContext());

        private ArrayExecutionContext()
        {
        }

        public List<uint> KeyCache = new List<uint>();
        public JsValue[] CallArray3 = new JsValue[3];

        public static ArrayExecutionContext Current => _executionContext.Value;
    }
}