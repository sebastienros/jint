using Esprima;
using Jint.Native;

namespace Jint.Runtime.Environments
{
    public readonly struct ExecutionContext
    {
        public ExecutionContext(
            Engine engine,
            LexicalEnvironment lexicalEnvironment, 
            LexicalEnvironment variableEnvironment, 
            JsValue thisBinding,
            string name = "eval")
        {
            LexicalEnvironment = lexicalEnvironment;
            VariableEnvironment = variableEnvironment;
            ThisBinding = thisBinding;
            this.Name = name;
            var lastNode = engine.GetLastSyntaxNode();
            if (lastNode != null)
            {
                this.Location = lastNode.Location;
            }
            else {
                this.Location = new Location();
            }
        }

        public readonly LexicalEnvironment LexicalEnvironment;
        public readonly LexicalEnvironment VariableEnvironment;
        public readonly JsValue ThisBinding;

        public readonly string Name;

        public Location Location { get; }

        public ExecutionContext UpdateLexicalEnvironment(Engine engine, LexicalEnvironment newEnv)
        {
            return new ExecutionContext(engine, newEnv, VariableEnvironment, ThisBinding);
        }
    }
}
