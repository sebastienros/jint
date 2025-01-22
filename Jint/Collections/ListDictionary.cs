#nullable disable

using System.Collections;
using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Collections;

internal sealed class ListDictionary<TValue> : DictionaryBase<TValue>, IEnumerable<KeyValuePair<Key, TValue>>
{
    private DictionaryNode _head;
    private int _count;
    private bool _checkExistingKeys;

    public ListDictionary(Key key, TValue value, bool checkExistingKeys)
    {
        _checkExistingKeys = checkExistingKeys;
        _head = new DictionaryNode
        {
            Key = key,
            Value = value
        };
        _count = 1;
    }

    public ListDictionary(DictionaryNode head, bool checkExistingKeys)
    {
        _checkExistingKeys = checkExistingKeys;
        _head = head;
        _count = 1;
    }

    public override ref TValue GetValueRefOrNullRef(Key key)
    {
        DictionaryNode node;
        for (node = _head; node != null; node = node.Next)
        {
            if (node.Key == key)
            {
                return ref node.Value;
            }
        }

        return ref Unsafe.NullRef<TValue>();
    }

    public override ref TValue GetValueRefOrAddDefault(Key key, out bool exists)
    {
        DictionaryNode last = null;
        DictionaryNode node;
        var checkExistingKeys = _checkExistingKeys;
        for (node = _head; node != null; node = node.Next)
        {
            var oldKey = node.Key;
            if (checkExistingKeys && oldKey == key)
            {
                exists = true;
                return ref node.Value;
            }

            last = node;
        }

        exists = false;
        return ref AddNode(key, default, last).Value;
    }

    public override int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    public bool Add(Key key, TValue value, bool tryAdd = false)
    {
        DictionaryNode last = null;
        DictionaryNode node;
        var checkExistingKeys = _checkExistingKeys;
        for (node = _head; node != null; node = node.Next)
        {
            var oldKey = node.Key;
            if (checkExistingKeys && oldKey == key)
            {
                if (tryAdd)
                {
                    return false;
                }
                ExceptionHelper.ThrowArgumentException();
            }

            last = node;
        }

        AddNode(key, value, last);
        return true;
    }

    /// <summary>
    /// Adds a new item and expects key to not exist.
    /// </summary>
    public void AddDangerous(Key key, TValue value)
    {
        DictionaryNode node;
        for (node = _head; node != null; node = node.Next)
        {
            if (node.Next is null)
            {
                AddNode(key, value, node);
                return;
            }
        }
    }


    private DictionaryNode AddNode(Key key, TValue value, DictionaryNode last)
    {
        var newNode = new DictionaryNode
        {
            Key = key,
            Value = value
        };
        if (_head is null)
        {
            _head = newNode;
        }
        else
        {
            last.Next = newNode;
        }
        _count++;
        return newNode;
    }

    public void Clear()
    {
        _count = 0;
        _head = null;
    }

    internal bool CheckExistingKeys
    {
        set => _checkExistingKeys = value;
    }

    public NodeEnumerator GetEnumerator()
    {
        return new NodeEnumerator(this);
    }

    IEnumerator<KeyValuePair<Key, TValue>> IEnumerable<KeyValuePair<Key, TValue>>.GetEnumerator()
    {
        return new NodeEnumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new NodeEnumerator(this);
    }

    public bool Remove(Key key)
    {
        DictionaryNode last = null;
        DictionaryNode node;
        for (node = _head; node != null; node = node.Next)
        {
            var oldKey = node.Key;
            if (oldKey == key)
            {
                break;
            }

            last = node;
        }

        if (node == null)
        {
            return false;
        }

        if (node == _head)
        {
            _head = node.Next;
        }
        else
        {
            last.Next = node.Next;
        }

        _count--;
        return true;
    }

    internal struct NodeEnumerator : IEnumerator<KeyValuePair<Key, TValue>>
    {
        private readonly ListDictionary<TValue> _list;
        private DictionaryNode _current;
        private bool _start;

        public NodeEnumerator(ListDictionary<TValue> list)
        {
            _list = list;
            _start = true;
            _current = null;
        }

        public KeyValuePair<Key, TValue> Current => new KeyValuePair<Key, TValue>(_current.Key, _current.Value);

        public bool MoveNext()
        {
            if (_start)
            {
                _current = _list._head;
                _start = false;
            }
            else if (_current != null)
            {
                _current = _current.Next;
            }

            return _current != null;
        }

        void IEnumerator.Reset()
        {
            _start = true;
            _current = null;
        }

        public void Dispose()
        {
        }

        object IEnumerator.Current => _current;
    }

    internal sealed class DictionaryNode
    {
        public Key Key;
        public TValue Value;
        public DictionaryNode Next;
    }
}
