using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Native;

namespace Jint.Runtime;

/// <summary>
/// A resume point in a <see cref="KeyedCollectionData"/> traversal. A default instance starts at the
/// beginning, so a traversal needs no explicit initialization.
/// </summary>
/// <remarks>
/// <see cref="Slot"/> is only meaningful while <see cref="Epoch"/> matches the collection's, which is
/// what makes a cursor survive a compaction: <see cref="NextSeq"/> names the resume point in terms the
/// entries carry themselves, so it stays exact however the slots move underneath.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal struct KeyedCollectionCursor
{
    /// <summary>The slot to resume examining at.</summary>
    internal int Slot;

    /// <summary>The collection's compaction epoch <see cref="Slot"/> was computed against.</summary>
    internal int Epoch;

    /// <summary>The sequence number the next entry visited must be at or after.</summary>
    internal long NextSeq;
}

/// <summary>
/// The ordered entry list behind a Map's [[MapData]] and a Set's [[SetData]]
/// (https://tc39.es/ecma262/#sec-map-objects, https://tc39.es/ecma262/#sec-set-objects), holding the
/// spec's <c>~empty~</c> tombstone directly.
/// </summary>
/// <remarks>
/// <para>
/// Every keyed-collection traversal the spec defines — <c>forEach</c>, the Map and Set iterators, and
/// the index-walking half of <c>difference</c>, <c>intersection</c>, <c>isDisjointFrom</c> and
/// <c>isSubsetOf</c> — walks the entry List <em>by index</em> while user code is free to mutate the
/// collection between two steps. The spec keeps that coherent by never removing an element: a deleted
/// entry becomes <c>~empty~</c> in place, so every surviving entry keeps its index and a suspended
/// traversal resumes exactly where it left off. Compacting on delete instead — which is what this type
/// used to do — shifts later entries left under a live cursor, which is how a traversal came to skip
/// an entry or visit one twice.
/// </para>
/// <para>
/// Storage is an append-only array of slots plus a <see cref="Dictionary{TKey,TValue}"/> from key to
/// slot, so membership, lookup and <em>delete</em> are all O(1); the delete previously cost a linear
/// scan plus a linear shift. Deleted slots are reclaimed at two points, both of which are invisible to
/// a traversal in flight (see <see cref="Next"/>): trailing tombstones are dropped as they appear, and
/// an append that finds the array full compacts in place when at least half the slots are dead rather
/// than doubling. So an <c>add</c>/<c>delete</c> churn loop cannot grow the List without bound — the
/// slot count stays within a constant factor of the live count — while an append still amortizes to
/// O(1).
/// </para>
/// <para>
/// Both reclaim paths move live entries, so both bump the compaction epoch; a cursor whose epoch is
/// stale re-derives its slot from <see cref="KeyedCollectionCursor.NextSeq"/> by binary search. Entry
/// sequence numbers are strictly increasing across slots, are never renumbered, and are 64-bit
/// precisely so that a suspended iterator cannot be defeated by a counter wrapping.
/// </para>
/// </remarks>
internal sealed class KeyedCollectionData
{
    [StructLayout(LayoutKind.Auto)]
    private struct Entry
    {
        /// <summary>The entry's key, or <see langword="null"/> for the spec's <c>~empty~</c>.</summary>
        internal JsValue? Key;

        /// <summary>The entry's value. Always <see langword="null"/> when the collection backs a Set.</summary>
        internal JsValue? Value;

        internal long Seq;
    }

    private static readonly Entry[] _emptyEntries = [];

    private Entry[] _entries;
    private int _slotCount;
    private int _tombstones;
    private long _nextSeq;
    private int _epoch;
    private readonly Dictionary<JsValue, int> _index;

    internal KeyedCollectionData() : this(0)
    {
    }

    internal KeyedCollectionData(int capacity)
    {
        _entries = capacity > 0 ? new Entry[capacity] : _emptyEntries;
        _index = new Dictionary<JsValue, int>(capacity, SameValueZeroComparer.Instance);
    }

    /// <summary>The number of live entries — the spec's SetDataSize.</summary>
    internal int Count => _index.Count;

    /// <summary>
    /// The number of elements in the spec's List, tombstones included. This is the bound every
    /// index-walking algorithm in the spec re-reads as "the number of elements in set.[[SetData]]".
    /// </summary>
    internal int SlotCount => _slotCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ContainsKey(JsValue key) => _index.ContainsKey(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetValue(JsValue key, [NotNullWhen(true)] out JsValue? value)
    {
        if (_index.TryGetValue(key, out var slot))
        {
            value = _entries[slot].Value!;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>The key at <paramref name="slot"/>, or <see langword="null"/> when it is a tombstone.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JsValue? KeyAt(int slot) => _entries[slot].Key;

    /// <summary>The value at <paramref name="slot"/>, which must not be a tombstone.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JsValue ValueAt(int slot) => _entries[slot].Value!;

    /// <summary>The spec's SetDataIndex: the slot holding <paramref name="key"/>, or -1.</summary>
    internal int IndexOf(JsValue key) => _index.TryGetValue(key, out var slot) ? slot : -1;

    /// <summary>
    /// Appends <paramref name="key"/> when it is absent, or overwrites the value of the existing
    /// entry — which per https://tc39.es/ecma262/#sec-map.prototype.set leaves it where it is.
    /// </summary>
    /// <returns><see langword="true"/> when a new entry was appended.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Set(JsValue key, JsValue? value)
    {
        if (_index.TryGetValue(key, out var slot))
        {
            _entries[slot].Value = value;
            return false;
        }

        Append(key, value);
        return true;
    }

    /// <summary>Appends <paramref name="key"/> unless it is already present.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(JsValue key)
    {
        if (!_index.ContainsKey(key))
        {
            Append(key, null);
        }
    }

    /// <summary>Appends without checking for an existing entry. The caller must know the key is absent.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Append(JsValue key, JsValue? value)
    {
        if (_slotCount == _entries.Length)
        {
            EnsureRoom();
        }

        ref var entry = ref _entries[_slotCount];
        entry.Key = key;
        entry.Value = value;
        entry.Seq = _nextSeq++;
        _index[key] = _slotCount;
        _slotCount++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Remove(JsValue key)
    {
        if (!_index.TryGetValue(key, out var slot))
        {
            return false;
        }

        _index.Remove(key);
        Tombstone(slot);
        return true;
    }

    /// <summary>
    /// Replaces the entry at <paramref name="slot"/> with the spec's <c>~empty~</c>. This is the
    /// "Set resultSetData[index] to ~empty~" step of <c>difference</c> and <c>symmetricDifference</c>.
    /// </summary>
    internal void RemoveAt(int slot)
    {
        var key = _entries[slot].Key;
        if (key is null)
        {
            return;
        }

        _index.Remove(key);
        Tombstone(slot);
    }

    private void Tombstone(int slot)
    {
        _entries[slot].Key = null;
        _entries[slot].Value = null;
        _tombstones++;

        if (slot != _slotCount - 1)
        {
            return;
        }

        // Trailing tombstones are free to drop: nothing can be appended into them, so no cursor can
        // ever need to resume inside the range. A cursor parked past the new end is handled by the
        // epoch bump below, which sends it back through NextSeq — without it, an entry appended after
        // the truncation would land at a slot the cursor has already passed and go unvisited.
        var entries = _entries;
        var count = _slotCount;
        do
        {
            count--;
            entries[count] = default;
            _tombstones--;
        } while (count > 0 && entries[count - 1].Key is null);

        _slotCount = count;
        _epoch++;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.clear — every entry becomes <c>~empty~</c>.
    /// </summary>
    /// <remarks>
    /// The spec preserves the List itself "because there may be existing Set Iterator objects that are
    /// suspended midway through iterating over that List", but a List of nothing but tombstones is
    /// indistinguishable from an empty one: a suspended cursor resumes by sequence number, every entry
    /// appended afterwards has a higher one than anything it has visited, and the spec's own index walk
    /// would reach exactly those entries after skipping the tombstones. So the slots are dropped, which
    /// is the "physically removing the entry from internal data structures" the delete note allows.
    /// </remarks>
    internal void Clear()
    {
        if (_slotCount == 0)
        {
            return;
        }

        Array.Clear(_entries, 0, _slotCount);
        _slotCount = 0;
        _tombstones = 0;
        _index.Clear();
        _epoch++;
    }

    /// <summary>
    /// A copy holding the live entries in order — the spec's "let resultSetData be a copy of
    /// set.[[SetData]]". Tombstones are not carried over; no algorithm that takes such a copy can tell,
    /// because each one either only appends to it or walks it skipping <c>~empty~</c> anyway.
    /// </summary>
    internal KeyedCollectionData Clone()
    {
        var clone = new KeyedCollectionData(Count);
        var entries = _entries;
        for (var i = 0; i < _slotCount; i++)
        {
            var key = entries[i].Key;
            if (key is not null)
            {
                clone.Append(key, entries[i].Value);
            }
        }

        return clone;
    }

    /// <summary>
    /// Drops every tombstone. Only safe on a collection no traversal can be suspended in — a result
    /// object a Set method is about to hand back and nothing else has seen yet.
    /// </summary>
    internal void TrimTombstones()
    {
        if (_tombstones > 0)
        {
            Compact();
        }
    }

    /// <summary>
    /// Advances <paramref name="cursor"/> to the next live entry and returns its slot, or -1 when the
    /// List is exhausted. Re-reads the slot count on every call, so an entry appended by user code
    /// running between two steps is visited, exactly as "Set entriesCount to the number of elements in
    /// entries" requires.
    /// </summary>
    internal int Next(ref KeyedCollectionCursor cursor)
    {
        if (cursor.Epoch != _epoch)
        {
            cursor.Slot = FindSlot(cursor.NextSeq);
            cursor.Epoch = _epoch;
        }

        var entries = _entries;
        var slotCount = _slotCount;
        for (var i = cursor.Slot; i < slotCount; i++)
        {
            var key = entries[i].Key;
            if (key is not null)
            {
                cursor.Slot = i + 1;
                cursor.NextSeq = entries[i].Seq + 1;
                return i;
            }
        }

        cursor.Slot = slotCount;
        return -1;
    }

    /// <summary>
    /// The first slot whose sequence number is at least <paramref name="seq"/>, or <see cref="SlotCount"/>.
    /// Sequence numbers increase strictly across slots — entries are only ever appended, and compaction
    /// preserves both their order and their numbers — so a plain binary search is exact.
    /// </summary>
    private int FindSlot(long seq)
    {
        var entries = _entries;
        var lo = 0;
        var hi = _slotCount;
        while (lo < hi)
        {
            var mid = (int) (((uint) lo + (uint) hi) >> 1);
            if (entries[mid].Seq < seq)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EnsureRoom()
    {
        // Reclaiming rather than doubling once half the slots are dead is what bounds the List: the
        // array can only grow while live entries occupy more than half of it, so the slot count stays
        // within a constant factor of the live count however hard a script churns add/delete.
        if (_tombstones > 0 && _tombstones >= _slotCount / 2)
        {
            Compact();
            if (_slotCount < _entries.Length)
            {
                return;
            }
        }

        Array.Resize(ref _entries, _entries.Length == 0 ? 4 : _entries.Length * 2);
    }

    private void Compact()
    {
        var entries = _entries;
        var target = 0;
        for (var i = 0; i < _slotCount; i++)
        {
            var key = entries[i].Key;
            if (key is null)
            {
                continue;
            }

            if (target != i)
            {
                entries[target] = entries[i];
                _index[key] = target;
            }

            target++;
        }

        Array.Clear(entries, target, _slotCount - target);
        _slotCount = target;
        _tombstones = 0;
        _epoch++;
    }

    internal Enumerator GetEnumerator() => new Enumerator(this);

    /// <summary>Walks the live entries in order, and stays coherent if the collection is mutated meanwhile.</summary>
    internal struct Enumerator
    {
        private readonly KeyedCollectionData _data;
        private KeyedCollectionCursor _cursor;
        private int _slot;

        internal Enumerator(KeyedCollectionData data)
        {
            _data = data;
            _cursor = default;
            _slot = -1;
        }

        internal bool MoveNext()
        {
            _slot = _data.Next(ref _cursor);
            return _slot >= 0;
        }

        internal JsValue Key => _data.KeyAt(_slot)!;

        internal JsValue Value => _data.ValueAt(_slot);
    }
}
