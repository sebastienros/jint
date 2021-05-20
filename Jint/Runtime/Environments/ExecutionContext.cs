#nullable enable

using Jint.Native.Generator;

namespace Jint.Runtime.Environments
{
    public readonly struct ExecutionContext
    {
        internal ExecutionContext(
            EnvironmentRecord lexicalEnvironment,
            EnvironmentRecord variableEnvironment,
            Generator? generator = null)
        {
            LexicalEnvironment = lexicalEnvironment;
            VariableEnvironment = variableEnvironment;
            Generator = generator;
        }

        public readonly EnvironmentRecord LexicalEnvironment;
        public readonly EnvironmentRecord VariableEnvironment;
        internal readonly Generator? Generator;

        public ExecutionContext UpdateLexicalEnvironment(EnvironmentRecord lexicalEnvironment)
        {
            return new ExecutionContext(lexicalEnvironment, VariableEnvironment, Generator);
        }

        public ExecutionContext UpdateVariableEnvironment(EnvironmentRecord variableEnvironment)
        {
            return new ExecutionContext(LexicalEnvironment, variableEnvironment, Generator);
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

        internal ExecutionContext UpdateGenerator(Generator generator)
        {
            return new ExecutionContext(LexicalEnvironment, VariableEnvironment, generator);
        }

        internal GeneratorKind GetGeneratorKind()
        {
            if (Generator is null)
            {
                return GeneratorKind.NonGenerator;
            }

            // TODO If generator has an [[AsyncGeneratorState]] internal slot, return async.

            return GeneratorKind.Sync;
        }
    }
}
