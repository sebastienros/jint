using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Collections;

/// <summary>
/// Stack for struct types.
/// </summary>
internal sealed class RefStack<T> : IEnumerable<T> where T : struct
{
    internal T[] _array;
    internal int _size;

    private const int DefaultCapacity = 2;

    public RefStack(int capacity = DefaultCapacity)
    {
        _array = new T[capacity];
        _size = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Peek()
    {
        return ref _array[_size - 1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Peek(int fromTop)
    {
        var index = _size - 1 - fromTop;
        return ref _array[index];
    }

    public T this[int index] => _array[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeek([NotNullWhen(true)] out T item)
    {
        if (_size > 0)
        {
            item = _array[_size - 1];
            return true;
        }

        item = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Pop()
    {
        if (_size == 0)
        {
            ExceptionHelper.ThrowInvalidOperationException("stack is empty");
        }

        _size--;
        return ref _array[_size];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(in T item)
    {
        if (_size == _array.Length)
        {
            EnsureCapacity(_size + 1);
        }

        _array[_size++] = item;
    }

    private void EnsureCapacity(int min)
    {
        var array = _array;
        if (array.Length < min)
        {
            var newCapacity = array.Length == 0
                ? DefaultCapacity
                : array.Length * 2;

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
                var newItems = new T[value];
                if (_size > 0)
                {
                    Array.Copy(_array, 0, newItems, 0, _size);
                }

                _array = newItems;
            }
            else
            {
                _array = [];
            }
        }
    }

    public void Clear()
    {
        _size = 0;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal struct Enumerator : IEnumerator<T>
    {
        private readonly RefStack<T> _stack;
        private int _index;
        private T? _currentElement;

        internal Enumerator(RefStack<T> stack)
        {
            _stack = stack;
            _index = -2;
            _currentElement = default;
        }

        public void Dispose()
        {
            _index = -1;
        }

        public bool MoveNext()
        {
            bool returnValue;
            if (_index == -2)
            {
                // First call to enumerator.
                _index = _stack._size - 1;
                returnValue = (_index >= 0);
                if (returnValue)
                {
                    _currentElement = _stack._array[_index];
                }
                return returnValue;
            }

            if (_index == -1)
            {
                // End of enumeration.
                return false;
            }

            returnValue = (--_index >= 0);
            if (returnValue)
            {
                _currentElement = _stack._array[_index];
            }
            else
            {
                _currentElement = default;
            }
            return returnValue;
        }

        public T Current => (T) _currentElement!;

        object? IEnumerator.Current => Current;

        void IEnumerator.Reset()
        {
            _index = -2;
            _currentElement = default;
        }
    }
}
