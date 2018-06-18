using Jint.Native;

namespace Jint.Runtime.Environments
{
    public readonly struct ExecutionContext
    {
        public ExecutionContext(LexicalEnvironment lexicalEnvironment, LexicalEnvironment variableEnvironment, JsValue thisBinding)
        {
            LexicalEnvironment = lexicalEnvironment;
            VariableEnvironment = variableEnvironment;
            ThisBinding = thisBinding;
        }

        public readonly LexicalEnvironment LexicalEnvironment;
        public readonly LexicalEnvironment VariableEnvironment;
        public readonly JsValue ThisBinding;

        public ExecutionContext UpdateLexicalEnvironment(LexicalEnvironment newEnv)
        {
            return new ExecutionContext(newEnv, VariableEnvironment, ThisBinding);
        }
    }
}
