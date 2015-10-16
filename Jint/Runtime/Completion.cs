using Jint.Native;

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

        public Completion(string type, JsValue? value, string identifier)
        {
            _type = type;
            _value = value;
            _identifier = identifier;
        }

        private string _type;
        private JsValue? _value;
        private string _identifier;

        public string Type { get { return _type; } }
        public JsValue? Value { get { return _value; } }
        public string Identifier { get { return _identifier; } }

        public JsValue GetValueOrDefault()
        {
            return Value.HasValue ? Value.Value : Undefined.Instance;
        }

        public Jint.Parser.Location Location { get; set; }
    }
}
