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

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getthisenvironment
        /// </summary>
        internal EnvironmentRecord GetThisEnvironment()
        {
            // The loop will always terminate because the list of environments always
            // ends with the global environment which has a this binding.
            var lex = LexicalEnvironment;
            while (true)
            {
                var envRec = lex._record;
                var exists = envRec.HasThisBinding();
                if (exists)
                {
                    return envRec;
                }

                var outer = lex._outer;
                lex = outer;
            }
        }


    }
}
