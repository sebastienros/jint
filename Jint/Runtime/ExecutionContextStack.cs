using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Runtime.Environments;

namespace Jint.Runtime
{
    internal sealed class ExecutionContextStack
    {
        private readonly RefStack<ExecutionContext> _stack;

        public ExecutionContextStack(int capacity)
        {
            _stack = new RefStack<ExecutionContext>(capacity);
        }

        public void ReplaceTopLexicalEnvironment(EnvironmentRecord newEnv)
        {
            var array = _stack._array;
            var size = _stack._size;
            array[size - 1] = array[size - 1].UpdateLexicalEnvironment(newEnv);
        }

        public void ReplaceTopVariableEnvironment(EnvironmentRecord newEnv)
        {
            var array = _stack._array;
            var size = _stack._size;
            array[size - 1] = array[size - 1].UpdateVariableEnvironment(newEnv);
        }

        public void ReplaceTopPrivateEnvironment(PrivateEnvironmentRecord? newEnv)
        {
            var array = _stack._array;
            var size = _stack._size;
            array[size - 1] = array[size - 1].UpdatePrivateEnvironment(newEnv);
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
                var context = array[i];
                if (context.ScriptOrModule is not null)
                {
                    return context.ScriptOrModule;
                }
            }

            return null;
        }
    }
}
