using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Jint.Runtime
{
    internal class MruPropertyCache2<TKey, TValue> where TKey : class where TValue : class
    {
        private Dictionary<TKey, TValue> _dictionary;
        private bool _set;
        private TKey _key;
        private TValue _value;

        public TValue this[TKey key]
        {
            get
            {
                if (_set && key.Equals(_key))
                {
                    return _value;
                }

                return _dictionary != null ? _dictionary[key] : null;
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

        public void Add(TKey key, TValue value)
        {
            EnsureInitialized(key);
            _set = true;
            _key = key;
            _value = value;

            if (_dictionary != null)
            {
                _dictionary.Add(key, value);
            }
        }

        public void Clear()
        {
            _set = false;
            _key = default(TKey);
            _value = null;

            _dictionary?.Clear();
        }

        public bool ContainsKey(TKey key)
        {
            if (_set && key.Equals(_key))
            {
                return true;
            }

            return _dictionary != null && _dictionary.ContainsKey(key);
        }

        public IEnumerable<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            if (_dictionary == null)
            {
                if (_set)
                {
                    yield return new KeyValuePair<TKey, TValue>(_key, _value);
                }

                yield break;
            }

            foreach (var pair in _dictionary)
            {
                yield return pair;
            }
        }

        public bool Remove(TKey key)
        {
            bool removed = false;
            if (_set && key.Equals(_key))
            {
                _set = false;
                _key = default(TKey);
                _value = null;
                removed = true;
            }

            _dictionary?.Remove(key);
            return removed;
        }

        public bool TryGetValue(TKey key, out TValue value)
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
        private void EnsureInitialized(TKey key)
        {
            if (_set && !Equals(_key, key))
            {
                if (_dictionary == null)
                {
                    _dictionary = new Dictionary<TKey, TValue>();
                }
                _dictionary[_key] = _value;
            }
        }

        public TKey[] GetKeys()
        {
            int size = _set ? 1 : 0;
            if (_dictionary != null)
            {
                size += _dictionary.Count;
            }

            var keys = new TKey[size];
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