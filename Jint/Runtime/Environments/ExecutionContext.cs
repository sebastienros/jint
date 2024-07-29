using Jint.Native.Function;

using Jint.Native.Generator;

namespace Jint.Runtime.Environments;

internal readonly struct ExecutionContext
{
    internal ExecutionContext(
        IScriptOrModule? scriptOrModule,
        Environment lexicalEnvironment,
        Environment variableEnvironment,
        PrivateEnvironment? privateEnvironment,
        Realm realm,
        GeneratorInstance? generator = null,
        Function? function = null,
        ParserOptions? parserOptions = null)
    {
        ScriptOrModule = scriptOrModule;
        LexicalEnvironment = lexicalEnvironment;
        VariableEnvironment = variableEnvironment;
        PrivateEnvironment = privateEnvironment;
        Realm = realm;
        Function = function;
        Generator = generator;
        ParserOptions = parserOptions;
    }

    public readonly IScriptOrModule? ScriptOrModule;
    public readonly Environment LexicalEnvironment;
    public readonly Environment VariableEnvironment;
    public readonly PrivateEnvironment? PrivateEnvironment;
    public readonly Realm Realm;
    public readonly Function? Function;
    public readonly GeneratorInstance? Generator;
    public readonly ParserOptions? ParserOptions;

    public bool Suspended => Generator?._generatorState == GeneratorState.SuspendedYield;

    public ExecutionContext UpdateLexicalEnvironment(Environment lexicalEnvironment)
    {
        return new ExecutionContext(ScriptOrModule, lexicalEnvironment, VariableEnvironment, PrivateEnvironment, Realm, Generator, Function);
    }

    public ExecutionContext UpdateVariableEnvironment(Environment variableEnvironment)
    {
        return new ExecutionContext(ScriptOrModule, LexicalEnvironment, variableEnvironment, PrivateEnvironment, Realm, Generator, Function);
    }

    public ExecutionContext UpdatePrivateEnvironment(PrivateEnvironment? privateEnvironment)
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
    internal Environment GetThisEnvironment()
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
