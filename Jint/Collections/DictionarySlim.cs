#nullable disable

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Jint.Collections;

/// <summary>
/// DictionarySlim&lt;string, TValue> is similar to Dictionary&lt;TKey, TValue> but optimized in three ways:
/// 1) It allows access to the value by ref replacing the common TryGetValue and Add pattern.
/// 2) It does not store the hash code (assumes it is cheap to equate values).
/// 3) It does not accept an equality comparer (assumes Object.GetHashCode() and Object.Equals() or overridden implementation are cheap and sufficient).
/// <para>
/// It additionally enumerates in <em>insertion order</em>, which the type it was derived from does not:
/// this backs a JavaScript object's own symbol-keyed properties, and
/// <see href="https://tc39.es/ecma262/#sec-ordinaryownpropertykeys">OrdinaryOwnPropertyKeys</see> requires
/// them "in ascending chronological order of property creation". A removed entry therefore leaves a
/// tombstone rather than joining a free list an add would pop from — reusing a vacated slot would hand a
/// key an enumeration position older than its creation. See <see cref="Resize"/> for how the tombstones
/// are reclaimed.
/// </para>
/// </summary>
[DebuggerDisplay("Count = {Count}")]
internal sealed class DictionarySlim<TKey, TValue> : IReadOnlyCollection<KeyValuePair<TKey, TValue>> where TKey : IEquatable<TKey>
{
    // We want to initialize without allocating arrays. We also want to avoid null checks.
    // Array.Empty would give divide by zero in modulo operation. So we use static one element arrays.
    // The first add will cause a resize replacing these with real arrays of three elements.
    // Arrays are wrapped in a class to avoid being duplicated for each <TKey, TValue>
    private static readonly Entry[] InitialEntries = new Entry[1];

    /// <summary>
    /// <see cref="Entry.next"/> of a removed entry. Live entries carry -1 (end of bucket chain) or a
    /// 0-based index, and a slot never written carries 0, so this is the one value below -1.
    /// </summary>
    private const int Tombstone = -2;

    // Number of live entries.
    private int _count;
    // High-water mark: _entries[0.._lastIndex) are live entries and tombstones, in insertion order.
    private int _lastIndex;
    // 1-based index into _entries; 0 means empty
    private int[] _buckets;
    private Entry[] _entries;

    [DebuggerDisplay("({key}, {value})->{next}")]
    [StructLayout(LayoutKind.Auto)]
    private struct Entry
    {
        public TKey key;
        public TValue value;
        // 0-based index of next entry in chain: -1 means end of chain,
        // Tombstone (-2) means this entry was removed and holds no key.
        public int next;
    }

    public DictionarySlim()
    {
        _buckets = HashHelpers.SizeOneIntArray;
        _entries = InitialEntries;
    }

    public DictionarySlim(int capacity)
    {
        if (capacity < 2)
            capacity = 2; // 1 would indicate the dummy array
        capacity = HashHelpers.PowerOf2(capacity);
        _buckets = new int[capacity];
        _entries = new Entry[capacity];
    }

    public int Count => _count;

    /// <summary>
    /// Clears the dictionary. Note that this invalidates any active enumerators.
    /// </summary>
    public void Clear()
    {
        _count = 0;
        _lastIndex = 0;
        _buckets = HashHelpers.SizeOneIntArray;
        _entries = InitialEntries;
    }

    public bool ContainsKey(TKey key)
    {
        Entry[] entries = _entries;
        for (int i = _buckets[key.GetHashCode() & (_buckets.Length - 1)] - 1;
             (uint) i < (uint) entries.Length; i = entries[i].next)
        {
            if (key.Equals(entries[i].key))
                return true;
        }

        return false;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        Entry[] entries = _entries;
        for (int i = _buckets[key.GetHashCode() & (_buckets.Length - 1)] - 1;
             (uint) i < (uint) entries.Length; i = entries[i].next)
        {
            if (key.Equals(entries[i].key))
            {
                value = entries[i].value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public bool Remove(TKey key)
    {
        Entry[] entries = _entries;
        int bucketIndex = key.GetHashCode() & (_buckets.Length - 1);
        int entryIndex = _buckets[bucketIndex] - 1;

        int lastIndex = -1;
        while (entryIndex != -1)
        {
            Entry candidate = entries[entryIndex];
            if (candidate.key.Equals(key))
            {
                if (lastIndex != -1)
                {   // Fixup preceding element in chain to point to next (if any)
                    entries[lastIndex].next = candidate.next;
                }
                else
                {   // Fixup bucket to new head (if any)
                    _buckets[bucketIndex] = candidate.next + 1;
                }

                entries[entryIndex] = default;
                entries[entryIndex].next = Tombstone;

                _count--;

                // Removing the newest entry can hand its slot straight back without disturbing the order
                // of anything older, which is what makes add-then-remove churn free of compaction.
                if (entryIndex == _lastIndex - 1)
                {
                    _lastIndex = entryIndex;
                }

                return true;
            }
            lastIndex = entryIndex;
            entryIndex = candidate.next;
        }

        return false;
    }

    // Not safe for concurrent _reads_ (at least, if either of them add)
    // For concurrent reads, prefer TryGetValue(key, out value)
    /// <summary>
    /// Gets the value for the specified key, or, if the key is not present,
    /// adds an entry and returns the value by ref. This makes it possible to
    /// add or update a value in a single look up operation.
    /// </summary>
    /// <param name="key">Key to look for</param>
    /// <returns>Reference to the new or existing value</returns>
    public ref TValue GetOrAddValueRef(TKey key)
    {
        Entry[] entries = _entries;
        int bucketIndex = key.GetHashCode() & (_buckets.Length - 1);
        for (int i = _buckets[bucketIndex] - 1;
             (uint) i < (uint) entries.Length; i = entries[i].next)
        {
            if (key.Equals(entries[i].key))
                return ref entries[i].value;
        }

        return ref AddKey(key, bucketIndex);
    }

    public ref TValue this[TKey key]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref GetOrAddValueRef(key);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ref TValue AddKey(TKey key, int bucketIndex)
    {
        // Always appends: the new entry is the newest key, and enumeration is entry order.
        Entry[] entries = _entries;
        if (_lastIndex == entries.Length || entries.Length == 1)
        {
            entries = Resize();
            bucketIndex = key.GetHashCode() & (_buckets.Length - 1);
            // entry indexes of surviving entries were not reordered by Resize
        }

        var entryIndex = _lastIndex++;
        entries[entryIndex].key = key;
        entries[entryIndex].next = _buckets[bucketIndex] - 1;
        _buckets[bucketIndex] = entryIndex + 1;
        _count++;
        return ref entries[entryIndex].value;
    }

    /// <summary>
    /// Makes room at the top of the entry array, which is the only place an add may write.
    /// <para>
    /// With no removals this is the plain doubling it has always been. Otherwise the tombstones left by
    /// <see cref="Remove"/> are squeezed out — in place when the live entries fit in half the capacity,
    /// and into a doubled array when they do not. Compaction preserves relative order, so it is invisible
    /// to enumeration; insisting on the half is what keeps adds amortized O(1), since every call here is
    /// followed by at least <c>capacity / 2</c> adds before the next one.
    /// </para>
    /// <para>
    /// The half is measured rather than chosen. It is consulted only by a table that has both left
    /// tombstones and filled its array, and because capacities are powers of two it does not tune the
    /// cost continuously — it decides which power of two such a table settles on, and the interval
    /// between compactions is then the capacity minus the live count. A quarter settles a rotating key
    /// set at twice its live count and costs a compaction every <c>n</c> adds; a half settles it at four
    /// times and costs one every <c>3n</c>. Measured against slot reuse that is +34.8% and +8.9%. Going
    /// on to a three-quarters erases the rest and doubles the ceiling again, which is the trade this
    /// stops short of; #3285 carries the curve and the memory figures at every setting.
    /// </para>
    /// </summary>
    private Entry[] Resize()
    {
        Debug.Assert(_lastIndex == _entries.Length || _entries.Length == 1);

        var entries = _entries;
        var count = _count;
        var lastIndex = _lastIndex;

        Entry[] newEntries;
        if (count == lastIndex || count + 1 > entries.Length - (entries.Length >> 1))
        {
            var newSize = entries.Length * 2;
            if ((uint) newSize > int.MaxValue) // uint cast handles overflow
                throw new InvalidOperationException("Capacity Overflow");

            newEntries = new Entry[newSize];
            _buckets = new int[newSize];
        }
        else
        {
            newEntries = entries;
            Array.Clear(_buckets, 0, _buckets.Length);
        }

        if (count == lastIndex)
        {
            Array.Copy(entries, 0, newEntries, 0, count);
        }
        else
        {
            var write = 0;
            for (var read = 0; read < lastIndex; read++)
            {
                if (entries[read].next != Tombstone)
                {
                    newEntries[write++] = entries[read];
                }
            }

            Debug.Assert(write == count);

            if (ReferenceEquals(newEntries, entries))
            {
                // Release the keys and values the compacted-away tail still references.
                Array.Clear(entries, count, lastIndex - count);
            }
        }

        _lastIndex = count;
        _entries = newEntries;

        var buckets = _buckets;
        while (count-- > 0)
        {
            int bucketIndex = newEntries[count].key.GetHashCode() & (buckets.Length - 1);
            newEntries[count].next = buckets[bucketIndex] - 1;
            buckets[bucketIndex] = count + 1;
        }

        return newEntries;
    }

    /// <summary>
    /// Gets an enumerator over the dictionary
    /// </summary>
    public Enumerator GetEnumerator() => new Enumerator(this); // avoid boxing

    /// <summary>
    /// Gets an enumerator over the dictionary
    /// </summary>
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() =>
        new Enumerator(this);

    /// <summary>
    /// Gets an enumerator over the dictionary
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly DictionarySlim<TKey, TValue> _dictionary;
        private int _index;
        private int _count;
        private KeyValuePair<TKey, TValue> _current;

        internal Enumerator(DictionarySlim<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _index = 0;
            _count = _dictionary._count;
            _current = default;
        }

        public bool MoveNext()
        {
            if (_count == 0)
            {
                _current = default;
                return false;
            }

            _count--;

            // Skips the tombstones removals left behind; the live entries between them are still in
            // insertion order, which is what this type exists to preserve.
            while (_dictionary._entries[_index].next == Tombstone)
                _index++;

            _current = new KeyValuePair<TKey, TValue>(
                _dictionary._entries[_index].key,
                _dictionary._entries[_index++].value);
            return true;
        }

        public KeyValuePair<TKey, TValue> Current => _current;

        object IEnumerator.Current => _current;

        void IEnumerator.Reset()
        {
            _index = 0;
            _count = _dictionary._count;
            _current = default;
        }

        public void Dispose() { }
    }

    internal static class HashHelpers
    {
        internal static readonly int[] SizeOneIntArray = new int[1];

        internal static int PowerOf2(int v)
        {
            if ((v & (v - 1)) == 0) return v;
            int i = 2;
            while (i < v) i <<= 1;
            return i;
        }
    }
}
