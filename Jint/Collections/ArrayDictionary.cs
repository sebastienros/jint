#nullable disable

using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Collections;

/// <summary>
/// Array-backed, insertion-ordered dictionary used by <see cref="HybridDictionary{TValue}"/> for the
/// small element-count regime (below the cutover to <see cref="StringDictionarySlim{TValue}"/>).
/// <para>
/// It replaces a per-entry singly-linked list: a single contiguous <c>Entry[]</c> means one allocation
/// instead of one node object per key, far better cache locality on the linear scan, and no per-node
/// header overhead. When the owner knows the final size up front (object literals, scopes) the array is
/// sized exactly, so there is zero growth churn for the common case.
/// </para>
/// </summary>
[DebuggerDisplay("Count = {Count}")]
internal sealed class ArrayDictionary<TValue> : DictionaryBase<TValue>, IEnumerable<KeyValuePair<Key, TValue>>
{
    private struct Entry
    {
        public Key Key;
        public TValue Value;
    }

    private Entry[] _entries;
    private int _count;
    private bool _checkExistingKeys;

    public ArrayDictionary(int capacity, bool checkExistingKeys)
    {
        _checkExistingKeys = checkExistingKeys;
        _entries = capacity > 0 ? new Entry[capacity] : System.Array.Empty<Entry>();
    }

    public ArrayDictionary(Key key, TValue value, bool checkExistingKeys)
    {
        _checkExistingKeys = checkExistingKeys;
        _entries = new Entry[1];
        _entries[0].Key = key;
        _entries[0].Value = value;
        _count = 1;
    }

    public override int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    public override ref TValue GetValueRefOrNullRef(Key key)
    {
        var entries = _entries;
        var count = _count;
        for (var i = 0; i < count; i++)
        {
            if (entries[i].Key == key)
            {
                return ref entries[i].Value;
            }
        }

        return ref Unsafe.NullRef<TValue>();
    }

    public override ref TValue GetValueRefOrAddDefault(Key key, out bool exists)
    {
        if (_checkExistingKeys)
        {
            var entries = _entries;
            var count = _count;
            for (var i = 0; i < count; i++)
            {
                if (entries[i].Key == key)
                {
                    exists = true;
                    return ref entries[i].Value;
                }
            }
        }

        exists = false;
        return ref AddKey(key);
    }

    public bool Add(Key key, TValue value, bool tryAdd = false)
    {
        if (_checkExistingKeys)
        {
            var entries = _entries;
            var count = _count;
            for (var i = 0; i < count; i++)
            {
                if (entries[i].Key == key)
                {
                    if (tryAdd)
                    {
                        return false;
                    }

                    Throw.ArgumentException();
                }
            }
        }

        AddKey(key) = value;
        return true;
    }

    /// <summary>
    /// Adds a new item and expects key to not exist.
    /// </summary>
    public void AddDangerous(Key key, TValue value)
    {
        AddKey(key) = value;
    }

    private ref TValue AddKey(Key key)
    {
        var entries = _entries;
        if (_count == entries.Length)
        {
            entries = Resize();
        }

        ref var entry = ref entries[_count];
        entry.Key = key;
        _count++;
        return ref entry.Value;
    }

    private Entry[] Resize()
    {
        var newCapacity = _entries.Length == 0 ? 1 : _entries.Length * 2;
        var newEntries = new Entry[newCapacity];
        System.Array.Copy(_entries, newEntries, _count);
        _entries = newEntries;
        return newEntries;
    }

    public bool Remove(Key key)
    {
        var entries = _entries;
        var count = _count;
        for (var i = 0; i < count; i++)
        {
            if (entries[i].Key == key)
            {
                // shift the tail down by one to preserve insertion order
                System.Array.Copy(entries, i + 1, entries, i, count - i - 1);
                _count--;
                entries[_count] = default; // release reference for GC
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Empties the dictionary, keeping the already-grown backing array so a pooled owner that refills it
    /// with a similar key count avoids reallocating. Mirrors
    /// <see cref="StringDictionarySlim{TValue}.ClearPreservingCapacity"/>.
    /// </summary>
    public void ClearPreservingCapacity()
    {
        if (_count > 0)
        {
            System.Array.Clear(_entries, 0, _count);
            _count = 0;
        }
    }

    public void Clear() => ClearPreservingCapacity();

    internal bool CheckExistingKeys
    {
        set => _checkExistingKeys = value;
    }

    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<KeyValuePair<Key, TValue>> IEnumerable<KeyValuePair<Key, TValue>>.GetEnumerator() => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    internal struct Enumerator : IEnumerator<KeyValuePair<Key, TValue>>
    {
        private readonly ArrayDictionary<TValue> _dictionary;
        private int _index;

        internal Enumerator(ArrayDictionary<TValue> dictionary)
        {
            _dictionary = dictionary;
            _index = -1;
        }

        public bool MoveNext() => ++_index < _dictionary._count;

        public KeyValuePair<Key, TValue> Current
        {
            get
            {
                ref var entry = ref _dictionary._entries[_index];
                return new KeyValuePair<Key, TValue>(entry.Key, entry.Value);
            }
        }

        object IEnumerator.Current => Current;

        void IEnumerator.Reset() => _index = -1;

        public void Dispose()
        {
        }
    }
}
