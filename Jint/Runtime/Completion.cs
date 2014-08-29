using Jint.Native;
using System;

namespace Jint.Runtime
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.9
    /// </summary>
    public class Completion
    {
        public static string Normal = "normal";
        public static string Break = "break";
        public static string Continue = "continue";
        public static string Return = "return";
        public static string Throw = "throw";
		public Exception ExceptionLocation { get; set; }

        public Completion(string type, JsValue? value, string identifier, Exception ex=null)
        {
            Type = type;
            Value = value;
            Identifier = identifier;
			ExceptionLocation = ex;
        }

        public string Type { get; private set; }
        public JsValue? Value { get; private set; }
        public string Identifier { get; private set; }

        public JsValue GetValueOrDefault()
        {
            return Value.HasValue ? Value.Value : Undefined.Instance;
        }
    }
}
