using System;
using System.Runtime.CompilerServices;
using Jint.Runtime.Environments;

namespace Jint.Runtime
{
    internal sealed class ExecutionContextStack
    {
        private ExecutionContext[] _array;
        private int _size;

        private const int DefaultCapacity = 2;

        public ExecutionContextStack(int capacity)
        {
            _array = new ExecutionContext[capacity];
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
            if (_size == _array.Length)
            {
                EnsureCapacity(_size + 1);
            }
            _array[_size++] = item;
        }

        private void EnsureCapacity(int min)
        {
            if (_array.Length < min)
            {
                int newCapacity = _array.Length == 0
                    ? DefaultCapacity
                    : _array.Length * 2;

                if (newCapacity < min)
                {
                    newCapacity = min;
                }
                Resize(newCapacity);
            }
        }

        private void Resize(int value)
        {
            if (value != _array.Length)
            {
                if (value > 0)
                {
                    var newItems = new ExecutionContext[value];
                    if (_size > 0)
                    {
                        Array.Copy(_array, 0, newItems, 0, _size);
                    }

                    _array = newItems;
                }
                else
                {
                    _array = ArrayExt.Empty<ExecutionContext>();
                }
            }
        }

        public void ReplaceTopLexicalEnvironment(LexicalEnvironment newEnv)
        {
            _array[_size - 1] = _array[_size - 1].UpdateLexicalEnvironment(newEnv);
        }
    }
}