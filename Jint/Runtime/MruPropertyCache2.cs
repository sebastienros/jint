using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Jint.Runtime
{
    internal class MruPropertyCache2<TValue> where TValue : class
    {
        private Dictionary<string, TValue> _dictionary;
        private string _key;
        private TValue _value;

        public TValue this[string key]
        {
            get
            {
                if (_key != null && _key == key)
                {
                    return _value;
                }

                return _dictionary != null ? _dictionary[key] : null;
            }

            set
            {
                EnsureInitialized(key);
                _key = key;
                _value = value;

                if (_dictionary != null)
                {
                    _dictionary[key] = value;
                }
            }
        }

        public int Count
        {
            get
            {
                int count = _key != null ? 1 : 0;
                if (_dictionary != null)
                {
                    count += _dictionary.Count;
                }

                return count;
            }
        }

        public void Add(string key, TValue value)
        {
            EnsureInitialized(key);
            _key = key;
            _value = value;

            if (_dictionary != null)
            {
                _dictionary.Add(key, value);
            }
        }

        public void Clear()
        {
            _key = default(string);
            _value = null;

            _dictionary?.Clear();
        }

        public bool ContainsKey(string key)
        {
            if (_key != null && _key == key)
            {
                return true;
            }

            return _dictionary != null && _dictionary.ContainsKey(key);
        }

        public IEnumerable<KeyValuePair<string, TValue>> GetEnumerator()
        {
            if (_dictionary == null)
            {
                if (_key != null)
                {
                    yield return new KeyValuePair<string, TValue>(_key, _value);
                }

                yield break;
            }

            foreach (var pair in _dictionary)
            {
                yield return pair;
            }
        }

        public bool Remove(string key)
        {
            bool removed = false;
            if (_key != null && _key == key)
            {
                _key = null;
                _value = null;
                removed = true;
            }

            removed |= _dictionary?.Remove(key) ?? false;
            return removed;
        }

        public bool TryGetValue(string key, out TValue value)
        {
            if (_key != null && _key == key)
            {
                value = _value;
                return true;
            }

            if (_dictionary == null)
            {
                value = null;
                return false;
            }

            if (_dictionary.TryGetValue(key, out value))
            {
                _key = key;
                _value = value;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureInitialized(string key)
        {
            if (_key != null && _key != key)
            {
                if (_dictionary == null)
                {
                    _dictionary = new Dictionary<string, TValue>();
                }
                _dictionary[_key] = _value;
            }
        }

        public string[] GetKeys()
        {
            int size = _key != null ? 1 : 0;
            if (_dictionary != null)
            {
                size += _dictionary.Count;
            }

            var keys = new string[size];
            int n = 0;
            if (_key != null)
            {
                keys[n++] = _key;
            }
            if (_dictionary != null)
            {
                foreach (var key in _dictionary.Keys)
                {
                    keys[n++] = key;
                }
            }

            return keys;
        }
    }
}