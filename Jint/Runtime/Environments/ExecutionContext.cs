using System.Runtime.InteropServices;
using Jint.Native.Function;

namespace Jint.Runtime.Environments;

[StructLayout(LayoutKind.Auto)]
internal readonly struct ExecutionContext
{
    internal ExecutionContext(
        IScriptOrModule? scriptOrModule,
        EnvironmentRecord lexicalEnvironment,
        EnvironmentRecord variableEnvironment,
        PrivateEnvironmentRecord? privateEnvironment,
        Realm realm,
        FunctionInstance? function = null)
    {
        ScriptOrModule = scriptOrModule;
        LexicalEnvironment = lexicalEnvironment;
        VariableEnvironment = variableEnvironment;
        PrivateEnvironment = privateEnvironment;
        Realm = realm;
        Function = function;
    }

    public readonly IScriptOrModule? ScriptOrModule;
    public readonly EnvironmentRecord LexicalEnvironment;
    public readonly EnvironmentRecord VariableEnvironment;
    public readonly PrivateEnvironmentRecord? PrivateEnvironment;
    public readonly Realm Realm;
    public readonly FunctionInstance? Function;

    public ExecutionContext UpdateLexicalEnvironment(EnvironmentRecord lexicalEnvironment)
    {
        return new ExecutionContext(ScriptOrModule, lexicalEnvironment, VariableEnvironment, PrivateEnvironment, Realm, Function);
    }

    public ExecutionContext UpdateVariableEnvironment(EnvironmentRecord variableEnvironment)
    {
        return new ExecutionContext(ScriptOrModule, LexicalEnvironment, variableEnvironment, PrivateEnvironment, Realm, Function);
    }

    public ExecutionContext UpdatePrivateEnvironment(PrivateEnvironmentRecord? privateEnvironment)
    {
        return new ExecutionContext(ScriptOrModule, LexicalEnvironment, VariableEnvironment, privateEnvironment, Realm, Function);
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
}
