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
/// key an enumeration position older than its creation.
/// </para>
/// <para>
/// The entries occupy a window <c>[_firstIndex, _lastIndex)</c> over the array rather than a prefix of it,
/// and that window may wrap the end of the array, so a removal at <em>either</em> end retires its slot
/// instead of leaving a hole: the top walks back when the newest key goes, and the base advances when the
/// oldest one does. A rotating key set — a fresh name in, the oldest one out, which is what an object used
/// as a bounded cache does — therefore never compacts at all. Only a removal from the middle leaves a
/// tombstone behind, and <see cref="Resize"/> is where those are reclaimed.
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
    // Base of the window: the oldest occupied slot, and a live entry whenever _count is not 0.
    private int _firstIndex;
    // The slot the next add writes. The window [_firstIndex, _lastIndex) holds the live entries and the
    // tombstones between them, in insertion order, and may wrap the end of the array.
    private int _lastIndex;
    // The one bound the add path tests: the exclusive limit on _lastIndex. It is the capacity while the
    // window runs to the end of the array, the base while the window wraps below it, and 0 for the shared
    // dummy array, which nothing may write to. So it is what tells a full window from an empty one, since
    // both leave _firstIndex equal to _lastIndex.
    private int _limit;
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
        // _limit is left at 0 so that the first add makes room: the dummy arrays are shared statics.
    }

    public DictionarySlim(int capacity)
    {
        if (capacity < 2)
            capacity = 2; // 1 would indicate the dummy array
        capacity = HashHelpers.PowerOf2(capacity);
        _buckets = new int[capacity];
        _entries = new Entry[capacity];
        _limit = capacity;
    }

    public int Count => _count;

    /// <summary>
    /// Clears the dictionary. Note that this invalidates any active enumerators.
    /// </summary>
    public void Clear()
    {
        _count = 0;
        _firstIndex = 0;
        _lastIndex = 0;
        _limit = 0;
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
                // of anything older, which is what makes add-then-remove churn free of
                // compaction. Only the top moves, so the bound the add path tests does not change: the
                // free space it reopens is the free space it was already allowed to write. It closes the
                // window where it stands when that entry was also the last live one, which is an empty
                // window like any other — the next add reopens it on the slot it just gave back.
                //
                // The test is written inverted, and everything that is not this case is behind one call,
                // because that is what the JIT lays out as a comparison this path falls straight through
                // into its single store and its return — the shape the high-water mark had, with no
                // taken branch on it. Spelling it as an if/else, or asking about the base here as well,
                // moves the store out of line and puts a taken branch on the commonest delete there is.
                if (entryIndex != _lastIndex - 1)
                {
                    return RetireSlot(entries, entryIndex);
                }

                _lastIndex = entryIndex;
                return true;
            }
            lastIndex = entryIndex;
            entryIndex = candidate.next;
        }

        return false;
    }

    /// <summary>
    /// Retires a just-vacated slot that was not the newest: the window's base advances when it was the
    /// oldest — which is what makes a rotating key set free of compaction — and nothing moves when it was
    /// neither.
    /// <para>
    /// It advances past any tombstones behind that slot as well, so that the base keeps pointing at a live
    /// entry: otherwise it would come to rest on a tombstone and the next removal of the oldest would no
    /// longer recognize itself, and one delete from the middle would disarm the fast path for good. A
    /// removal from the middle leaves its tombstone where it is, because reclaiming it would move the keys
    /// above it and their position <em>is</em> their creation order; those are what <see cref="Resize"/>
    /// squeezes out.
    /// </para>
    /// </summary>
    /// <returns>
    /// Always <see langword="true" />, which is <see cref="Remove"/>'s own answer: returning it lets the
    /// caller hand this half of the work over with a tail call instead of a call and a join, which is what
    /// keeps the newest-entry path straight-line.
    /// </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool RetireSlot(Entry[] entries, int retired)
    {
        // Neither end: the tombstone stays where it is, and only Resize reclaims it. Splitting the walk
        // off keeps this half small, so the commoner of the two answers costs a compare and a return.
        return retired != _firstIndex || RetireBase(entries, retired);
    }

    /// <summary>
    /// Advances the window's base past the slot the oldest entry has just vacated, and past any tombstones
    /// behind it. Its <see langword="true" /> is <see cref="RetireSlot"/>'s.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool RetireBase(Entry[] entries, int retired)
    {
        var length = entries.Length;
        if (_count == 0)
        {
            // The last live entry is gone, so the window goes back to the base of the array and the next
            // add starts at slot 0 again; every slot it held was cleared as it was removed, which is what
            // makes starting over safe. It is also what ends the walk below: with nothing live left there
            // would be nothing for it to stop on.
            _firstIndex = 0;
            _lastIndex = 0;
            _limit = length;
            return true;
        }

        var first = (retired + 1) & (length - 1);
        while (entries[first].next == Tombstone)
        {
            first = (first + 1) & (length - 1);
        }

        _firstIndex = first;

        // The window wraps once its top is at or below the new base, and then the base is the slot an add
        // may not reach; otherwise the free space runs to the end of the array.
        _limit = _lastIndex <= first ? first : length;
        return true;
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
        // Always appends at the top of the window: the new entry is the newest key, and enumeration is
        // window order. _limit is the one thing standing between _lastIndex and a slot it may not write,
        // so the window costs this path a field read where the mark cost an array length read — and one
        // comparison, where the mark needed two.
        Entry[] entries = _entries;
        if (_lastIndex == _limit)
        {
            entries = MakeRoom();
            bucketIndex = key.GetHashCode() & (_buckets.Length - 1);
        }

        var entryIndex = _lastIndex++;
        entries[entryIndex].key = key;
        entries[entryIndex].next = _buckets[bucketIndex] - 1;
        _buckets[bucketIndex] = entryIndex + 1;
        _count++;
        return ref entries[entryIndex].value;
    }

    /// <summary>
    /// Makes room at the top of the window, which is the only place an add may write, for a window that
    /// has reached its bound.
    /// <para>
    /// There are three ways to have reached it, and only the last one costs anything. A window that has run
    /// to the end of the array while its base has moved off slot 0 <em>wraps</em>, which is a pair of field
    /// writes and no data movement at all: that is a rotating key set, and it is why one never compacts. A
    /// window based at slot 0 is the shape the type has always had, and <see cref="Resize"/> is the code it
    /// has always run. Anything else is a full window that does not start at slot 0, which
    /// <see cref="ResizeWindow"/> grows or squeezes.
    /// </para>
    /// </summary>
    private Entry[] MakeRoom()
    {
        var entries = _entries;
        if (_firstIndex != 0)
        {
            if (_lastIndex == entries.Length)
            {
                // Free space at the bottom of the array, which the window rotated away from: it continues
                // there, and the base is now the slot the top may not reach.
                _lastIndex = 0;
                _limit = _firstIndex;
                return entries;
            }

            return ResizeWindow(entries);
        }

        return Resize();
    }

    /// <summary>
    /// Makes room for a window based at slot 0, which is every table that has never had its oldest entry
    /// removed — and so the only shape this had before the window existed.
    /// <para>
    /// With no removals this is the plain doubling it has always been. Otherwise the tombstones left by a
    /// removal from the middle are squeezed out — in place when the live entries fit in half the capacity,
    /// and into a doubled array when they do not. Compaction preserves relative order, so it is invisible
    /// to enumeration; insisting on the half is what keeps adds amortized O(1), since every call here is
    /// followed by at least <c>capacity / 2</c> adds before the next one.
    /// </para>
    /// <para>
    /// The half is measured rather than chosen. It is consulted only by a table that has both left
    /// tombstones and filled its array, and because capacities are powers of two it does not tune the
    /// cost continuously — it decides which power of two such a table settles on, and the interval
    /// between compactions is then the capacity minus the live count. It was measured on a rotating key
    /// set, which no longer reaches this method at all; what still does is a table whose oldest entry
    /// stays put while the keys above it churn. #3285 carries that curve and the memory figures at every
    /// setting, and #3315 is why the shape it was measured on is now free.
    /// </para>
    /// </summary>
    private Entry[] Resize()
    {
        Debug.Assert(_firstIndex == 0);
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
        _limit = newEntries.Length;

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
    /// Makes room for a full window that does not start at slot 0, which is a table whose base has moved
    /// and whose middle has since filled with tombstones — a rotating key set with a key pinned under it.
    /// The threshold is <see cref="Resize"/>'s, measured there.
    /// <para>
    /// Growing is the chance to unwrap, so the survivors land at the bottom of the doubled array in window
    /// order and the window starts over at slot 0; when there is nothing to squeeze out that is two
    /// <see cref="Array.Copy(Array, int, Array, int, int)"/> runs, the tail of the old array and then its
    /// head, and taking them the other way round would reverse the keys around the wrap point. Compacting
    /// in place instead leaves the base where it is and moves the survivors down towards it, so the write
    /// cursor can never overtake the read cursor — which is what makes a wrapped window safe to squeeze
    /// without a second array, and why it cannot be rebased to slot 0 in place.
    /// </para>
    /// </summary>
    private Entry[] ResizeWindow(Entry[] entries)
    {
        var count = _count;
        var length = entries.Length;
        var mask = length - 1;
        var first = _firstIndex;

        Debug.Assert(first != 0);
        Debug.Assert(_lastIndex == first);
        // A bound below the capacity is only ever set on a window that holds a live entry, so the window
        // reaching it is always a full one and never a closed one, which the squeeze below relies on.
        Debug.Assert(count != 0);

        if (count == length || count + 1 > length - (length >> 1))
        {
            var newSize = length * 2;
            if ((uint) newSize > int.MaxValue) // uint cast handles overflow
                throw new InvalidOperationException("Capacity Overflow");

            var newEntries = new Entry[newSize];
            _buckets = new int[newSize];

            if (count == length)
            {
                var toEnd = length - first;
                Array.Copy(entries, first, newEntries, 0, toEnd);
                Array.Copy(entries, 0, newEntries, toEnd, first);
            }
            else
            {
                var target = 0;
                var source = first;
                for (var n = 0; n < length; n++)
                {
                    if (entries[source].next != Tombstone)
                    {
                        newEntries[target++] = entries[source];
                    }

                    source = (source + 1) & mask;
                }

                Debug.Assert(target == count);
            }

            _entries = newEntries;
            _firstIndex = 0;
            _lastIndex = count;
            _limit = newSize;
            RebuildChains(newEntries, _buckets, 0, count);
            return newEntries;
        }

        Array.Clear(_buckets, 0, _buckets.Length);

        var write = first;
        var read = first;
        for (var n = 0; n < length; n++)
        {
            if (entries[read].next != Tombstone)
            {
                if (write != read)
                {
                    entries[write] = entries[read];
                }

                write = (write + 1) & mask;
            }

            read = (read + 1) & mask;
        }

        Debug.Assert(write == ((first + count) & mask));

        // Release the keys and values the squeezed-away slots still reference, wrapping at the end of the
        // array the way the window itself does.
        var vacated = length - count;
        var toArrayEnd = length - write;
        if (vacated <= toArrayEnd)
        {
            Array.Clear(entries, write, vacated);
        }
        else
        {
            Array.Clear(entries, write, toArrayEnd);
            Array.Clear(entries, 0, vacated - toArrayEnd);
        }

        _lastIndex = write;
        _limit = write <= first ? first : length;
        RebuildChains(entries, _buckets, first, count);
        return entries;
    }

    /// <summary>
    /// Rehangs <paramref name="count"/> entries from <paramref name="first"/> onto empty buckets, walking
    /// the window in order and wrapping at the end of the array. A bucket and a chain link are physical
    /// slot indexes that encode no position in the window, which is why the moving base costs lookup
    /// nothing and why only a resize has to do this at all.
    /// </summary>
    private static void RebuildChains(Entry[] entries, int[] buckets, int first, int count)
    {
        var mask = entries.Length - 1;
        var bucketMask = buckets.Length - 1;
        var index = first;
        for (var n = 0; n < count; n++)
        {
            var bucketIndex = entries[index].key.GetHashCode() & bucketMask;
            entries[index].next = buckets[bucketIndex] - 1;
            buckets[bucketIndex] = index + 1;
            index = (index + 1) & mask;
        }
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
            _index = dictionary._firstIndex;
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

            // Walks the window from its base, wrapping at the end of the array, and skips the tombstones a
            // removal from the middle left behind; the live entries between them are still in insertion
            // order, which is what this type exists to preserve. The live count is what ends the walk, so
            // it never has to test the window's top — and starting at the base is what spares it the run of
            // tombstones a table that keeps dropping its oldest key used to leave below one.
            var entries = _dictionary._entries;
            var mask = entries.Length - 1;
            var index = _index;
            while (entries[index].next == Tombstone)
            {
                index = (index + 1) & mask;
            }

            _current = new KeyValuePair<TKey, TValue>(entries[index].key, entries[index].value);
            _index = (index + 1) & mask;
            return true;
        }

        public KeyValuePair<TKey, TValue> Current => _current;

        object IEnumerator.Current => _current;

        void IEnumerator.Reset()
        {
            _index = _dictionary._firstIndex;
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
