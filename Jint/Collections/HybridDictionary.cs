#nullable disable

using System.Collections;
using System.Runtime.CompilerServices;

namespace Jint.Collections;

internal sealed class HybridDictionary<TValue> : IEngineDictionary<Key, TValue>, IEnumerable<KeyValuePair<Key, TValue>>
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

    public HybridDictionary(StringDictionarySlim<TValue> dictionary)
    {
        _checkExistingKeys = true;
        _dictionary = dictionary;
    }

    public ref TValue this[Key key]
    {
        get
        {
            if (_dictionary != null)
            {
                return ref _dictionary[key];
            }

            if (_list != null)
            {
                if (_list.Count >= CutoverPoint - 1)
                {
                    return ref SwitchToDictionary(key);
                }

                return ref _list[key];
            }

            var head = new ListDictionary<TValue>.DictionaryNode { Key = key, Value = default };
            _list = new ListDictionary<TValue>(head, _checkExistingKeys);
            return ref head.Value;
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

    public ref TValue GetValueRefOrNullRef(Key key)
    {
        if (_dictionary != null)
        {
            return ref _dictionary.GetValueRefOrNullRef(key);
        }

        if (_list != null)
        {
            return ref _list.GetValueRefOrNullRef(key);
        }

        return ref Unsafe.NullRef<TValue>();
    }

    public ref TValue GetValueRefOrAddDefault(Key key, out bool exists)
    {
        if (_dictionary != null)
        {
            return ref _dictionary.GetValueRefOrAddDefault(key, out exists);
        }

        if (_list != null)
        {
            return ref _list.GetValueRefOrAddDefault(key, out exists);
        }

        var head = new ListDictionary<TValue>.DictionaryNode
        {
            Key = key,
        };

        _list = new ListDictionary<TValue>(head, _checkExistingKeys);
        exists = false;
        return ref head.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetOrUpdateValue<TState>(Key key, Func<TValue, TState, TValue> updater, TState state)
    {
        ref var currentValue = ref GetValueRefOrAddDefault(key, out _);
        currentValue = updater(currentValue, state);
    }

    private bool SwitchToDictionary(Key key, TValue value, bool tryAdd, int capacity = InitialDictionarySize)
    {
        SwitchToDictionary(capacity);

        if (tryAdd)
        {
            return _dictionary.TryAdd(key, value);
        }

        _dictionary[key] = value;
        return true;
    }

    private ref TValue SwitchToDictionary(Key key, int capacity = InitialDictionarySize)
    {
        SwitchToDictionary(capacity);
        return ref _dictionary[key];
    }

    private void SwitchToDictionary(int capacity = InitialDictionarySize)
    {
        var dictionary = new StringDictionarySlim<TValue>(capacity);

        if (_list is not null)
        {
            foreach (var pair in _list)
            {
                dictionary[pair.Key] = pair.Value;
            }
        }

        _dictionary = dictionary;
        _list = null;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _dictionary?.Count ?? _list?.Count ?? 0;
    }

    public void EnsureCapacity(int capacity)
    {
        if (_dictionary is not null)
        {
            // not implemented yet
            return;
        }

        if (capacity >= CutoverPoint)
        {
            SwitchToDictionary(capacity);
        }
    }


    public bool TryAdd(Key key, TValue value)
    {
        if (_dictionary != null)
        {
            return _dictionary.TryAdd(key, value);
        }

        _list ??= new ListDictionary<TValue>(key, value, _checkExistingKeys);

        if (_list.Count + 1 >= CutoverPoint)
        {
            return SwitchToDictionary(key, value, tryAdd: true);
        }

        return _list.Add(key, value, tryAdd: true);
    }

    /// <summary>
    /// Adds a new item and expects key to not exist.
    /// </summary>
    public void AddDangerous(Key key, TValue value)
    {
        if (_dictionary != null)
        {
            _dictionary.AddDangerous(key, value);
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
                    SwitchToDictionary(key) = value;
                }
                else
                {
                    _list.AddDangerous(key, value);
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
        ref var valueRefOrNullRef = ref GetValueRefOrNullRef(key);
        return !Unsafe.IsNullRef(ref valueRefOrNullRef);
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
