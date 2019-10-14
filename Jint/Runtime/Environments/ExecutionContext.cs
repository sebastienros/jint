using Jint.Native;

namespace Jint.Runtime.Environments
{
    public class ExecutionContext
    {
        public ExecutionContext(
            LexicalEnvironment lexicalEnvironment, 
            LexicalEnvironment variableEnvironment, 
            JsValue thisBinding)
        {
            LexicalEnvironment = lexicalEnvironment;
            VariableEnvironment = variableEnvironment;
            ThisBinding = thisBinding;
        }

        public LexicalEnvironment LexicalEnvironment { get; }
        public LexicalEnvironment VariableEnvironment { get; }
        public JsValue ThisBinding { get; }

    }
}
