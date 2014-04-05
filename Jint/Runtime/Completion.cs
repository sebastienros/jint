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

        public Completion(string type, JsValue? value, string identifier, Jint.Parser.Location location = null)
        {
            Type = type;
            Value = value;
            Identifier = identifier;
            InitLocation(location);
        }

        public string Type { get; private set; }
        public JsValue? Value { get; private set; }
        public string Identifier { get; private set; }
        public Jint.Parser.Location Location;

        public JsValue GetValueOrDefault()
        {
            return Value.HasValue ? Value.Value : Undefined.Instance;
        }

        public void InitLocation(Jint.Parser.Location location)
        {
            if (location != null && !this.Location.IsInitialized)
            {
                this.Location = location.Clone();
            }
        }

        public void InitLocation(Jint.Parser.Ast.Statement statement)
        {
            if (statement != null)
            {
                InitLocation(statement.Location);
            }
        }
    }
}
