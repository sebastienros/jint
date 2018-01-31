using Esprima;
using Jint.Native;

namespace Jint.Runtime
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.9
    /// </summary>
    public class Completion
    {
        public const string Normal = "normal";
        public const string Break = "break";
        public const string Continue = "continue";
        public const string Return = "return";
        public const string Throw = "throw";

        public static readonly Completion Empty = new Completion(Normal, null, null);
        public static readonly Completion EmptyUndefined = new Completion(Normal, Undefined.Instance, null);

        public Completion(string type, JsValue value, string identifier)
        {
            Type = type;
            Value = value;
            Identifier = identifier;
        }

        public string Type { get; }
        public JsValue Value { get; }
        public string Identifier { get; }

        public JsValue GetValueOrDefault()
        {
            return Value != null ? Value : Undefined.Instance;
        }

        public bool IsAbrupt()
        {
            return Type != Normal;
        }

        public Location Location { get; set; }
    }
}
