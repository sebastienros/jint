namespace Jint.Runtime.Environments
{
    public readonly struct ExecutionContext
    {
        public ExecutionContext(
            LexicalEnvironment lexicalEnvironment,
            LexicalEnvironment variableEnvironment)
        {
            LexicalEnvironment = lexicalEnvironment;
            VariableEnvironment = variableEnvironment;
        }

        public readonly LexicalEnvironment LexicalEnvironment;
        public readonly LexicalEnvironment VariableEnvironment;

        public ExecutionContext UpdateLexicalEnvironment(LexicalEnvironment lexicalEnvironment)
        {
            return new ExecutionContext(lexicalEnvironment, VariableEnvironment);
        }

        public ExecutionContext UpdateVariableEnvironment(LexicalEnvironment variableEnvironment)
        {
            return new ExecutionContext(LexicalEnvironment, variableEnvironment);
        }
    }
}
