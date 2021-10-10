using Jint.Native.Function;

using Jint.Native.Generator;

namespace Jint.Runtime.Environments
{
    internal readonly struct ExecutionContext
    {
        internal ExecutionContext(
            IScriptOrModule? scriptOrModule,
            EnvironmentRecord lexicalEnvironment,
            EnvironmentRecord variableEnvironment,
            PrivateEnvironmentRecord? privateEnvironment,
            Realm realm,
            GeneratorInstance? generator = null,
            FunctionInstance? function = null)
        {
            ScriptOrModule = scriptOrModule;
            LexicalEnvironment = lexicalEnvironment;
            VariableEnvironment = variableEnvironment;
            PrivateEnvironment = privateEnvironment;
            Realm = realm;
            Function = function;
            Generator = generator;
        }

        public readonly IScriptOrModule? ScriptOrModule;
        public readonly EnvironmentRecord LexicalEnvironment;
        public readonly EnvironmentRecord VariableEnvironment;
        public readonly PrivateEnvironmentRecord? PrivateEnvironment;
        public readonly Realm Realm;
        public readonly FunctionInstance? Function;
        public readonly GeneratorInstance? Generator;

        public bool Suspended => Generator?._generatorState == GeneratorState.SuspendedYield;

        public ExecutionContext UpdateLexicalEnvironment(EnvironmentRecord lexicalEnvironment)
        {
            return new ExecutionContext(ScriptOrModule, lexicalEnvironment, VariableEnvironment, PrivateEnvironment, Realm, Generator, Function);
        }

        public ExecutionContext UpdateVariableEnvironment(EnvironmentRecord variableEnvironment)
        {
            return new ExecutionContext(ScriptOrModule, LexicalEnvironment, variableEnvironment, PrivateEnvironment, Realm, Generator, Function);
        }

        public ExecutionContext UpdatePrivateEnvironment(PrivateEnvironmentRecord? privateEnvironment)
        {
            return new ExecutionContext(ScriptOrModule, LexicalEnvironment, VariableEnvironment, privateEnvironment, Realm, Generator, Function);
        }

        public ExecutionContext UpdateGenerator(GeneratorInstance generator)
        {
            return new ExecutionContext(ScriptOrModule, LexicalEnvironment, VariableEnvironment, PrivateEnvironment, Realm, generator, Function);
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
                if (lex is not null)
                {
                    if (lex.HasThisBinding())
                    {
                        return lex;

                    }

                    lex = lex._outerEnv;
                }
            }
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
