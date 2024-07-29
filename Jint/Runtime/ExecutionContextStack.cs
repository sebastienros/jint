using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native.Generator;
using Jint.Runtime.Environments;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime;

internal sealed class ExecutionContextStack
{
    private readonly RefStack<ExecutionContext> _stack;

    public ExecutionContextStack(int capacity)
    {
        _stack = new RefStack<ExecutionContext>(capacity);
    }

    public void ReplaceTopLexicalEnvironment(Environment newEnv)
    {
        var array = _stack._array;
        var size = _stack._size;
        ref var executionContext = ref array[size - 1];
        executionContext = executionContext.UpdateLexicalEnvironment(newEnv);
    }

    public void ReplaceTopVariableEnvironment(Environment newEnv)
    {
        var array = _stack._array;
        var size = _stack._size;
        ref var executionContext = ref array[size - 1];
        executionContext = executionContext.UpdateVariableEnvironment(newEnv);
    }

    public void ReplaceTopPrivateEnvironment(PrivateEnvironment? newEnv)
    {
        var array = _stack._array;
        var size = _stack._size;
        ref var executionContext = ref array[size - 1];
        executionContext = executionContext.UpdatePrivateEnvironment(newEnv);
    }

    public ref readonly ExecutionContext ReplaceTopGenerator(GeneratorInstance newEnv)
    {
        var array = _stack._array;
        var size = _stack._size;
        ref var executionContext = ref array[size - 1];
        executionContext = executionContext.UpdateGenerator(newEnv);
        return ref executionContext;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly ExecutionContext Peek() => ref _stack.Peek();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly ExecutionContext Peek(int fromTop) => ref _stack.Peek(fromTop);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(in ExecutionContext context) => _stack.Push(in context);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly ExecutionContext Pop() => ref _stack.Pop();

    public IScriptOrModule? GetActiveScriptOrModule()
    {
        var array = _stack._array;
        var size = _stack._size;
        for (var i = size - 1; i > -1; --i)
        {
            ref readonly var context = ref array[i];
            if (context.ScriptOrModule is not null)
            {
                return context.ScriptOrModule;
            }
        }

        return null;
    }

    public ParserOptions? GetActiveParserOptions()
    {
        var array = _stack._array;
        var size = _stack._size;
        for (var i = size - 1; i > -1; --i)
        {
            ref readonly var context = ref array[i];
            if (context.ParserOptions is not null)
            {
                return context.ParserOptions;
            }
        }

        return null;
    }
}