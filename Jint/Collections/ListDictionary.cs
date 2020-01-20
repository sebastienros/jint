using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Collections
{
    internal sealed class ListDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : IEquatable<TKey>
    {
        private DictionaryNode head;
        private int count;

        public ListDictionary()
        {
        }

        public TValue this[TKey key]
        {
            get
            {
                TryGetValue(key, out var value);
                return value;
            }
            set
            {
                DictionaryNode last = null;
                DictionaryNode node;
                for (node = head; node != null; node = node.Next)
                {
                    var oldKey = node.Key;
                    if (oldKey.Equals(key))
                    {
                        break;
                    }

                    last = node;
                }

                if (node != null)
                {
                    // Found it
                    node.Value = value;
                    return;
                }

                // Not found, so add a new one
                DictionaryNode newNode = new DictionaryNode();
                newNode.Key = key;
                newNode.Value = value;
                if (last != null)
                {
                    last.Next = newNode;
                }
                else
                {
                    head = newNode;
                }

                count++;
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            var node = head;
            while (node != null)
            {
                if (node.Key.Equals(key))
                {
                    value = node.Value;
                    return true;
                }

                node = node.Next;
            }

            value = default;
            return false;
        }

        public int Count => count;

        public void Add(TKey key, TValue value)
        {
            DictionaryNode last = null;
            DictionaryNode node;
            for (node = head; node != null; node = node.Next)
            {
                var oldKey = node.Key;
                if (oldKey.Equals(key))
                {
                    ExceptionHelper.ThrowArgumentException();
                }

                last = node;
            }

            // Not found, so add a new one
            DictionaryNode newNode = new DictionaryNode();
            newNode.Key = key;
            newNode.Value = value;
            if (last != null)
            {
                last.Next = newNode;
            }
            else
            {
                head = newNode;
            }

            count++;
        }

        public void Clear()
        {
            count = 0;
            head = null;
        }

        public bool ContainsKey(TKey key)
        {
            for (var node = head; node != null; node = node.Next)
            {
                var oldKey = node.Key;
                if (oldKey.Equals(key))
                {
                    return true;
                }
            }

            return false;
        }

        public NodeEnumerator GetEnumerator()
        {
            return new NodeEnumerator(this);
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return new NodeEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new NodeEnumerator(this);
        }

        public bool Remove(TKey key)
        {
            DictionaryNode last = null;
            DictionaryNode node;
            for (node = head; node != null; node = node.Next)
            {
                var oldKey = node.Key;
                if (oldKey.Equals(key))
                {
                    break;
                }

                last = node;
            }

            if (node == null)
            {
                return false;
            }

            if (node == head)
            {
                head = node.Next;
            }
            else
            {
                last.Next = node.Next;
            }

            count--;
            return true;
        }

        internal struct NodeEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly ListDictionary<TKey, TValue> _list;
            private DictionaryNode _current;
            private bool _start;

            public NodeEnumerator(ListDictionary<TKey, TValue> list)
            {
                _list = list;
                _start = true;
                _current = null;
            }

            public KeyValuePair<TKey, TValue> Current
            {
                get
                {
                    return new KeyValuePair<TKey, TValue>(_current.Key, _current.Value);
                }
            }

            public bool MoveNext()
            {
                if (_start)
                {
                    _current = _list.head;
                    _start = false;
                }
                else if (_current != null)
                {
                    _current = _current.Next;
                }

                return (_current != null);
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

        internal class DictionaryNode
        {
            public TKey Key;
            public TValue Value;
            public DictionaryNode Next;
        }
    }
}