using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jint.Runtime {
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

        private bool Find(TKey key, out KeyValuePair<TKey, TValue> result) {
            var cursor = _list.First;
            while(cursor != null) {
                if(key.Equals(cursor.Value.Key)) {
                    result = cursor.Value;
                    return true;
                }

                cursor = cursor.Next;
            }

            return false;
        }
        public TValue this[TKey key] {
            get {
                KeyValuePair<TKey, TValue> node;
                if(Find(key, out node)) {
                    return node.Value;
                }
                
                return _dictionary[key];
            }

            set {
                KeyValuePair<TKey, TValue> node;
                if (!Find(key, out node)) {
                    _list.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
                    _list.RemoveLast();
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
            KeyValuePair<TKey, TValue> node;
            if (!Find(item.Key, out node)) {
                _list.AddFirst(item);
                _list.RemoveLast();
            }

            _dictionary.Add(item);
        }

        public void Add(TKey key, TValue value) {
            KeyValuePair<TKey, TValue> node;
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
            KeyValuePair<TKey, TValue> node;
            if (Find(item.Key, out node)) {
                return true;
            }

            return _dictionary.Contains(item);
        }

        public bool ContainsKey(TKey key) {
            KeyValuePair<TKey, TValue> node;
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
            KeyValuePair<TKey, TValue> node;
            if (Find(item.Key, out node)) {
                _list.Remove(node);
            }

            return _dictionary.Remove(item);
        }

        public bool Remove(TKey key) {
            KeyValuePair<TKey, TValue> node;
            if (Find(key, out node)) {
                _list.Remove(node);
            }

            return _dictionary.Remove(key);
        }

        public bool TryGetValue(TKey key, out TValue value) {
            KeyValuePair<TKey, TValue> node;
            if (Find(key, out node)) {
                value = node.Value;
                return true;
            }

            return _dictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return _dictionary.GetEnumerator();
        }
    }
}
