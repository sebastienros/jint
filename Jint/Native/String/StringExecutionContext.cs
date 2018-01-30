using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Jint.Native.String
{
    /// <summary>
    /// Helper to cache common data structures when manipulating strings.
    /// </summary>
    internal class StringExecutionContext
    {
        private static readonly ThreadLocal<StringExecutionContext> _executionContext = new ThreadLocal<StringExecutionContext>(() => new StringExecutionContext());

        private StringBuilder _stringBuilder;
        private List<string> _splitSegmentList;
        private string[] _splitArray1;
        private JsValue[] _callArray3;

        private StringExecutionContext()
        {
        }

        public StringBuilder GetStringBuilder(int capacity)
        {
            if (_stringBuilder == null)
            {
                _stringBuilder = new StringBuilder(capacity);
            }
            else
            {
                _stringBuilder.EnsureCapacity(capacity);
            }

            return _stringBuilder;
        }

        public List<string> SplitSegmentList => _splitSegmentList = _splitSegmentList ?? new List<string>();
        public string[] SplitArray1 => _splitArray1 = _splitArray1 ?? new string[1];
        public JsValue[] CallArray3 => _callArray3 = _callArray3 ?? new JsValue[3];

        public static StringExecutionContext Current => _executionContext.Value;
    }
}