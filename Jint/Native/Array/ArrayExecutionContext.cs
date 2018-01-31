using System.Text;
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

        private StringBuilder _stringBuilder;

        private ArrayExecutionContext()
        {
        }

        public StringBuilder StringBuilder => _stringBuilder = _stringBuilder ?? new StringBuilder();

        public static ArrayExecutionContext Current => _executionContext.Value;
    }
}