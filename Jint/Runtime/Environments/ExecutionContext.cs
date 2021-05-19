#nullable enable

namespace Jint.Runtime.Environments
{
    public readonly struct ExecutionContext
    {
        internal ExecutionContext(
            EnvironmentRecord lexicalEnvironment,
            EnvironmentRecord variableEnvironment)
        {
            LexicalEnvironment = lexicalEnvironment;
            VariableEnvironment = variableEnvironment;
        }

        public readonly EnvironmentRecord LexicalEnvironment;
        public readonly EnvironmentRecord VariableEnvironment;

        public ExecutionContext UpdateLexicalEnvironment(EnvironmentRecord lexicalEnvironment)
        {
            return new ExecutionContext(lexicalEnvironment, VariableEnvironment);
        }

        public ExecutionContext UpdateVariableEnvironment(EnvironmentRecord variableEnvironment)
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
                if (lex != null)
                {
                    if (lex.HasThisBinding())
                    {
                        return lex;
                        
                    }

                    lex = lex._outerEnv;
                }
            }
        }
    }
}
