using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Jint.Runtime.Environments;

namespace Jint.Runtime
{
    internal sealed class ExecutionContextStack
    {
        private ExecutionContext[] _array;
        private uint _size;

        private const int DefaultCapacity = 4;

        public ExecutionContextStack()
        {
            _array = new ExecutionContext[4];
            _size = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly ExecutionContext Peek()
        {
            if (_size == 0)
            {
                ExceptionHelper.ThrowInvalidOperationException("stack is empty");
            }
            return ref _array[_size - 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pop()
        {
            if (_size == 0)
            {
                ExceptionHelper.ThrowInvalidOperationException("stack is empty");
            }
            _size--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(in ExecutionContext item)
        {
            if (_size == (uint)_array.Length)
            {
                var newSize = 2 * _array.Length;
                var newArray = new ExecutionContext[newSize];
                Array.Copy(_array, 0, newArray, 0, _size);
                _array = newArray;
            }

            _array[_size++] = item;
        }

        public void ReplaceTopLexicalEnvironment(Engine engine, LexicalEnvironment newEnv)
        {
            _array[_size - 1] = _array[_size - 1].UpdateLexicalEnvironment(engine, newEnv);
        }

        public int Count { get => _array.Length; }

        public IEnumerator<ExecutionContext> GetEnumerator()
        {
            foreach (var item in _array)
            {
                yield return item;
            }
        }
    }
}