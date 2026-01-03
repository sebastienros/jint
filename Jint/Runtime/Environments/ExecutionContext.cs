using Jint.Native.AsyncFunction;
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
        ParserOptions? parserOptions = null,
        AsyncFunctionInstance? asyncFunction = null)
    {
        ScriptOrModule = scriptOrModule;
        LexicalEnvironment = lexicalEnvironment;
        VariableEnvironment = variableEnvironment;
        PrivateEnvironment = privateEnvironment;
        Realm = realm;
        Function = function;
        Generator = generator;
        ParserOptions = parserOptions;
        AsyncFunction = asyncFunction;
    }

    public readonly IScriptOrModule? ScriptOrModule;
    public readonly Environment LexicalEnvironment;
    public readonly Environment VariableEnvironment;
    public readonly PrivateEnvironment? PrivateEnvironment;
    public readonly Realm Realm;
    public readonly Function? Function;
    public readonly GeneratorInstance? Generator;
    public readonly ParserOptions? ParserOptions;
    public readonly AsyncFunctionInstance? AsyncFunction;

    public bool Suspended => Generator?._generatorState == GeneratorState.SuspendedYield;

    public bool AsyncSuspended => AsyncFunction?._state == AsyncFunctionState.SuspendedAwait;

    /// <summary>
    /// Returns the active suspendable (Generator or AsyncFunction) if any.
    /// </summary>
    public ISuspendable? Suspendable
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => (ISuspendable?) Generator ?? AsyncFunction;
    }

    /// <summary>
    /// Whether the current execution context is suspended.
    /// True if either generator is suspended at yield or async function is suspended at await.
    /// </summary>
    public bool IsSuspended
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => Suspendable?.IsSuspended == true;
    }

    public ExecutionContext UpdateLexicalEnvironment(Environment lexicalEnvironment)
    {
        return new ExecutionContext(ScriptOrModule, lexicalEnvironment, VariableEnvironment, PrivateEnvironment, Realm, Generator, Function, ParserOptions, AsyncFunction);
    }

    public ExecutionContext UpdateVariableEnvironment(Environment variableEnvironment)
    {
        return new ExecutionContext(ScriptOrModule, LexicalEnvironment, variableEnvironment, PrivateEnvironment, Realm, Generator, Function, ParserOptions, AsyncFunction);
    }

    public ExecutionContext UpdatePrivateEnvironment(PrivateEnvironment? privateEnvironment)
    {
        return new ExecutionContext(ScriptOrModule, LexicalEnvironment, VariableEnvironment, privateEnvironment, Realm, Generator, Function, ParserOptions, AsyncFunction);
    }

    public ExecutionContext UpdateGenerator(GeneratorInstance generator)
    {
        return new ExecutionContext(ScriptOrModule, LexicalEnvironment, VariableEnvironment, PrivateEnvironment, Realm, generator, Function, ParserOptions, AsyncFunction);
    }

    public ExecutionContext UpdateAsyncFunction(AsyncFunctionInstance asyncFunction)
    {
        return new ExecutionContext(ScriptOrModule, LexicalEnvironment, VariableEnvironment, PrivateEnvironment, Realm, Generator, Function, ParserOptions, asyncFunction);
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
