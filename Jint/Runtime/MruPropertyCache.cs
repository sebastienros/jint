using System.Collections;
using System.Collections.Generic;

namespace Jint.Runtime
{
    public class MruPropertyCache<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private IDictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();
        private LinkedList<KeyValuePair<TKey, TValue>> _list;
        private uint _length;

        public MruPropertyCache(uint length) {
            _length = length;
            _list = new LinkedList<KeyValuePair<TKey, TValue>>();
            for(int i=0; i<length; i++) {
                _list.AddLast(new KeyValuePair<TKey, TValue>(default(TKey), default(TValue)));
            }
        }

        private bool Find(TKey key, out LinkedListNode<KeyValuePair<TKey, TValue>> result) {
            result = _list.First;
            while(result != null) {
                if(key.Equals(result.Value.Key)) {
                    return true;
                }

                result = result.Next;
            }

            return false;
        }
        public TValue this[TKey key] {
            get {
                LinkedListNode<KeyValuePair<TKey, TValue>> node;
                if(Find(key, out node)) {
                    return node.Value.Value;
                }
                
                return _dictionary[key];
            }

            set {
                LinkedListNode<KeyValuePair<TKey, TValue>> node;
                if (!Find(key, out node)) {
                    _list.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
                    _list.RemoveLast();
                }
                else
                {
                    node.Value = new KeyValuePair<TKey, TValue>(key, value);
                }

                _dictionary[key] = value;
            }
        }

        public int Count {
            get {
                return _dictionary.Count;
            }
        }

        public bool IsReadOnly {
            get {
                return _dictionary.IsReadOnly;
            }
        }

        public ICollection<TKey> Keys {
            get {
                return _dictionary.Keys;
            }
        }

        public ICollection<TValue> Values {
            get {
                return _dictionary.Values;
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item) {
            LinkedListNode<KeyValuePair<TKey, TValue>> node;
            if (!Find(item.Key, out node)) {
                _list.AddFirst(item);
                _list.RemoveLast();
            }

            _dictionary.Add(item);
        }

        public void Add(TKey key, TValue value) {
            LinkedListNode<KeyValuePair<TKey, TValue>> node;
            if (!Find(key, out node)) {
                _list.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
                _list.RemoveLast();
            }
            _dictionary.Add(key, value);
        }

        public void Clear() {
            _list.Clear();
            _dictionary.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) {
            LinkedListNode<KeyValuePair<TKey, TValue>> node;
            if (Find(item.Key, out node)) {
                return true;
            }

            return _dictionary.Contains(item);
        }

        public bool ContainsKey(TKey key) {
            LinkedListNode<KeyValuePair<TKey, TValue>> node;
            if (Find(key, out node)) {
                return true;
            }

            return _dictionary.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
            _dictionary.CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
            return _dictionary.GetEnumerator();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) {
            LinkedListNode<KeyValuePair<TKey, TValue>> node;
            if (Find(item.Key, out node))
            {
                _list.Remove(node);
            }

            return _dictionary.Remove(item);
        }

        public bool Remove(TKey key) {
            LinkedListNode<KeyValuePair<TKey, TValue>> node;
            if (Find(key, out node))
            {
                _list.Remove(node);
            }

            return _dictionary.Remove(key);
        }

        public bool TryGetValue(TKey key, out TValue value) {
            LinkedListNode<KeyValuePair<TKey, TValue>> node;
            if (Find(key, out node)) {
                value = node.Value.Value;
                return true;
            }

            return _dictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return _dictionary.GetEnumerator();
        }
    }
}
