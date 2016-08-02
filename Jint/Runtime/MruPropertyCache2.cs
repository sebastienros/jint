using System.Collections;
using System.Collections.Generic;

namespace Jint.Runtime
{
    public class MruPropertyCache2<TKey, TValue> : IDictionary<TKey, TValue> 
        where TValue:class
    {
        private IDictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();
        private bool _set;
        private TKey _key;
        private TValue _value;

        public MruPropertyCache2() {
        }

        public TValue this[TKey key] {
            get {
                if (_set && key.Equals(_key))
                {
                    return _value;
                }

                return _dictionary[key];
            }

            set {
                _set = true;
                _key = key;
                _value = value;

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
            _set = true;
            _key = item.Key;
            _value = item.Value;

            _dictionary.Add(item);
        }

        public void Add(TKey key, TValue value) {
            _set = true;
            _key = key;
            _value = value;

            _dictionary.Add(key, value);
        }

        public void Clear() {
            _set = false;
            _key = default(TKey);
            _value = null;

            _dictionary.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) {
            if(_set && item.Key.Equals(_key))
            {
                return true;
            }

            return _dictionary.Contains(item);
        }

        public bool ContainsKey(TKey key) {
            if (_set && key.Equals(_key))
            {
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
            if(_set && item.Key.Equals(_key))
            {
                _set = false;
                _key = default(TKey);
                _value = null;
            }

            return _dictionary.Remove(item);
        }

        public bool Remove(TKey key) {
            if (_set && key.Equals(_key))
            {
                _set = false;
                _key = default(TKey);
                _value = null;
            }

            return _dictionary.Remove(key);
        }

        public bool TryGetValue(TKey key, out TValue value) {
            if (_set && key.Equals(_key))
            {
                value = _value;
                return true;
            }

            return _dictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return _dictionary.GetEnumerator();
        }
    }
}
