#nullable disable

using System.Collections;
using System.Runtime.CompilerServices;

namespace Jint.Collections
{
    internal class HybridDictionary<TValue> : IEnumerable<KeyValuePair<Key, TValue>>
    {
        private const int CutoverPoint = 9;
        private const int InitialDictionarySize = 13;
        private const int FixedSizeCutoverPoint = 6;

        private readonly bool _checkExistingKeys;
        private ListDictionary<TValue> _list;
        internal StringDictionarySlim<TValue> _dictionary;

        public HybridDictionary() : this(0, checkExistingKeys: true)
        {
        }

        public HybridDictionary(int initialSize, bool checkExistingKeys)
        {
            _checkExistingKeys = checkExistingKeys;
            if (initialSize >= FixedSizeCutoverPoint)
            {
                _dictionary = new StringDictionarySlim<TValue>(initialSize);
            }
        }

        protected HybridDictionary(StringDictionarySlim<TValue> dictionary)
        {
            _checkExistingKeys = true;
            _dictionary = dictionary;
        }

        public TValue this[Key key]
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
                        SwitchToDictionary(key, value);
                    }
                    else
                    {
                        _list[key] = value;
                    }
                }
                else
                {
                    _list = new ListDictionary<TValue>(key, value, _checkExistingKeys);
                }
            }
        }

        public bool TryGetValue(Key key, out TValue value)
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

        public void SetOrUpdateValue<TState>(Key key, Func<TValue, TState, TValue> updater, TState state)
        {
            if (_dictionary != null)
            {
                _dictionary.SetOrUpdateValue(key, updater, state);
            }
            else if (_list != null)
            {
                _list.SetOrUpdateValue(key, updater, state);
            }
            else
            {
                _list = new ListDictionary<TValue>(key, updater(default, state), _checkExistingKeys);
            }
        }

        private void SwitchToDictionary(Key key, TValue value)
        {
            var dictionary = new StringDictionarySlim<TValue>(InitialDictionarySize);
            foreach (var pair in _list)
            {
                dictionary[pair.Key] = pair.Value;
            }

            dictionary[key] = value;
            _dictionary = dictionary;
            _list = null;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dictionary?.Count ?? _list?.Count ?? 0;
        }

        public void Add(Key key, TValue value)
        {
            if (_dictionary != null)
            {
                _dictionary.GetOrAddValueRef(key) = value;
            }
            else
            {
                if (_list == null)
                {
                    _list = new ListDictionary<TValue>(key, value, _checkExistingKeys);
                }
                else
                {
                    if (_list.Count + 1 >= CutoverPoint)
                    {
                        SwitchToDictionary(key, value);
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
            _dictionary?.Clear();
            _list?.Clear();
        }

        public bool ContainsKey(Key key)
        {
            if (_dictionary != null)
            {
                return _dictionary.ContainsKey(key);
            }

            if (_list != null)
            {
                return _list.ContainsKey(key);
            }

            return false;
        }

        IEnumerator<KeyValuePair<Key, TValue>> IEnumerable<KeyValuePair<Key, TValue>>.GetEnumerator()
        {
            if (_dictionary != null)
            {
                return _dictionary.GetEnumerator();
            }

            if (_list != null)
            {
                return _list.GetEnumerator();
            }

            return System.Linq.Enumerable.Empty<KeyValuePair<Key, TValue>>().GetEnumerator();

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

            return System.Linq.Enumerable.Empty<KeyValuePair<Key, TValue>>().GetEnumerator();
        }

        public bool Remove(Key key)
        {
            if (_dictionary != null)
            {
                return _dictionary.Remove(key);
            }

            return _list != null && _list.Remove(key);
        }

        /// <summary>
        /// Optimization when no need to check for existing items.
        /// </summary>
        public bool CheckExistingKeys
        {
            set
            {
                if (_list != null)
                {
                    _list.CheckExistingKeys = value;
                }
            }
        }
    }
}
