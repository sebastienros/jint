using System.Collections.Generic;
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

        private List<uint> _keyCache;
        private JsValue[] _callArray1;
        private JsValue[] _callArray2;
        private JsValue[] _callArray3;
        private JsValue[] _callArray4;
        private StringBuilder _stringBuilder;

        private ArrayExecutionContext()
        {
        }

        public List<uint> KeyCache => _keyCache = _keyCache ?? new List<uint>();

        public JsValue[] CallArray1 => _callArray1 = _callArray1 ?? new JsValue[1];
        public JsValue[] CallArray2 => _callArray2 = _callArray2 ?? new JsValue[2];
        public JsValue[] CallArray3 => _callArray3 = _callArray3 ?? new JsValue[3];
        public JsValue[] CallArray4 => _callArray4 = _callArray4 ?? new JsValue[4];

        public StringBuilder StringBuilder => _stringBuilder = _stringBuilder ?? new StringBuilder();

        public static ArrayExecutionContext Current => _executionContext.Value;
    }
}