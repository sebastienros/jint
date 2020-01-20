using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Jint.Collections
{
    internal sealed class HybridDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : IEquatable<TKey>
    {
        private const int CutoverPoint = 9;
        private const int InitialDictionarySize = 13;
        private const int FixedSizeCutoverPoint = 6;

        // Instance variables. This keeps the HybridDictionary very light-weight when empty
        private ListDictionary<TKey, TValue> _list;
        private Dictionary<TKey, TValue> _dictionary;

        public HybridDictionary()
        {
        }

        public HybridDictionary(int initialSize)
        {
            if (initialSize >= FixedSizeCutoverPoint)
            {
                _dictionary = new Dictionary<TKey, TValue>(initialSize);
            }
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
                if (_dictionary != null)
                {
                    _dictionary[key] = value;
                }
                else if (_list != null)
                {
                    if (_list.Count >= CutoverPoint - 1)
                    {
                        SwitchToDictionary();
                        _dictionary[key] = value;
                    }
                    else
                    {
                        _list[key] = value;
                    }
                }
                else
                {
                    _list = new ListDictionary<TKey, TValue>();
                    _list[key] = value;
                }
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_dictionary != null)
            {
                return _dictionary.TryGetValue(key, out value);
            }

            if (_list != null)
            {
                return _list.TryGetValue(key, out value);
            }

            value = default;
            return false;
        }

        private void SwitchToDictionary()
        {
            var dictionary = new Dictionary<TKey, TValue>(InitialDictionarySize);
            foreach (var pair in _list)
            {
                dictionary[pair.Key] = pair.Value;
            }
            _dictionary = dictionary;
            _list = null;
        }

        public int Count
        {
            get
            {
                if (_dictionary != null)
                {
                    return _dictionary.Count;
                }

                return _list?.Count ?? 0;
            }
        }

        public void Add(TKey key, TValue value)
        {
            if (_dictionary != null)
            {
                _dictionary.Add(key, value);
            }
            else
            {
                if (_list == null)
                {
                    _list = new ListDictionary<TKey, TValue>();
                    _list.Add(key, value);
                }
                else
                {
                    if (_list.Count + 1 >= CutoverPoint)
                    {
                        SwitchToDictionary();
                        _dictionary.Add(key, value);
                    }
                    else
                    {
                        _list.Add(key, value);
                    }
                }
            }
        }

        public void Clear()
        {
            if (_dictionary != null)
            {
                var dictionary = _dictionary;
                _dictionary = null;
                dictionary.Clear();
            }

            if (_list != null)
            {
                var cachedList = _list;
                _list = null;
                cachedList.Clear();
            }
        }

        public bool ContainsKey(TKey key)
        {
            if (_dictionary != null)
            {
                return _dictionary.ContainsKey(key);
            }

            var cachedList = _list;
            if (cachedList != null)
            {
                return cachedList.ContainsKey(key);
            }

            return false;
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            if (_dictionary != null)
            {
                return _dictionary.GetEnumerator();
            }

            if (_list != null)
            {
                return _list.GetEnumerator();
            }

            return Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();

        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (_dictionary != null)
            {
                return _dictionary.GetEnumerator();
            }

            if (_list != null)
            {
                return _list.GetEnumerator();
            }

            return Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            if (_dictionary != null)
            {
                return _dictionary.Remove(key);
            }

            return _list != null && _list.Remove(key);
        }
    }
}