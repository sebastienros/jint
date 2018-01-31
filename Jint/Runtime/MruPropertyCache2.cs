using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Jint.Runtime
{
    internal class MruPropertyCache2<TValue> where TValue : class
    {
        private Dictionary<string, TValue> _dictionary;
        private bool _set;
        private string _key;
        private TValue _value;

        public TValue this[string key]
        {
            get
            {
                if (_set && _key == key)
                {
                    return _value;
                }

                return _dictionary?[key];
            }

            set
            {
                EnsureInitialized(key);
                _set = true;
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
                int count = _set ? 1 : 0;
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
            _set = true;
            _key = key;
            _value = value;

            _dictionary?.Add(key, value);
        }

        public void Clear()
        {
            _set = false;
            _key = default(string);
            _value = null;

            _dictionary?.Clear();
        }

        public bool ContainsKey(string key)
        {
            if (_set && key.Equals(_key))
            {
                return true;
            }

            return _dictionary != null && _dictionary.ContainsKey(key);
        }

        public IEnumerable<KeyValuePair<string, TValue>> GetEnumerator()
        {
            if (_dictionary == null)
            {
                if (_set)
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
            if (_set && key.Equals(_key))
            {
                _set = false;
                _key = null;
                _value = null;
                removed = true;
            }

            _dictionary?.Remove(key);
            return removed;
        }

        public bool TryGetValue(string key, out TValue value)
        {
            if (_set && _key.Equals(key))
            {
                value = _value;
                return true;
            }

            if (_dictionary == null)
            {
                value = null;
                return false;
            }

            return _dictionary.TryGetValue(key, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureInitialized(string key)
        {
            if (_set && _key != key)
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
            int size = _set ? 1 : 0;
            if (_dictionary != null)
            {
                size += _dictionary.Count;
            }

            var keys = new string[size];
            int n = 0;
            if (_set)
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