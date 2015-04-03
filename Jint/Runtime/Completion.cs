using Jint.Native;

namespace Jint.Runtime
{
    using Jint.Parser.Ast;

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

        public Completion(string type, JsValue? value, string identifier) : this(type, value, identifier, null)
        {
        }

        public Completion(string type, JsValue? value, string identifier, Statement lastStatement)
        {
            Type = type;
            Value = value;
            Identifier = identifier;
            LastStatement = lastStatement;
        }

        public string Type { get; private set; }
        public JsValue? Value { get; private set; }
        public string Identifier { get; private set; }
        public Statement LastStatement { get; private set; }

        public JsValue GetValueOrDefault()
        {
            return Value.HasValue ? Value.Value : Undefined.Instance;
        }
    }
}
