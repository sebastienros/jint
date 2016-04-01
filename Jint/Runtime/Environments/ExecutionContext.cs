using Jint.Native;

namespace Jint.Runtime.Environments
{
    public sealed class ExecutionContext
    {
        public ExecutionContext(Engine engine, string name)
        {
            this.Name = name;
            var lastNode = engine.GetLastSyntaxNode();
            if (lastNode != null)
            {
                this.Location = lastNode.Location;
            }
        }

        public string Name { get; set; }
        public Parser.Location Location { get; set; }
        public LexicalEnvironment LexicalEnvironment { get; set; }
        public LexicalEnvironment VariableEnvironment { get; set; }
        public JsValue ThisBinding { get; set; }

    }
}
