// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Collections
{
    /// <summary>
    /// DictionarySlim<TKey, TValue> is similar to Dictionary<TKey, TValue> but optimized in three ways:
    /// 1) It allows access to the value by ref replacing the common TryGetValue and Add pattern.
    /// 2) It does not store the hash code (assumes it is cheap to equate values).
    /// 3) It does not accept an equality comparer (assumes Object.GetHashCode() and Object.Equals() or overridden implementation are cheap and sufficient).
    /// </summary>
    [DebuggerTypeProxy(typeof(DictionarySlimDebugView<>))]
    [DebuggerDisplay("Count = {Count}")]
    internal sealed class StringDictionarySlim<TValue>
    {
        // We want to initialize without allocating arrays. We also want to avoid null checks.
        // Array.Empty would give divide by zero in modulo operation. So we use static one element arrays.
        // The first add will cause a resize replacing these with real arrays of three elements.
        // Arrays are wrapped in a class to avoid being duplicated for each <TKey, TValue>
        private static readonly Entry[] InitialEntries = new Entry[1];
        private int _count;
        // 1-based index into _entries; 0 means empty
        private int[] _buckets;
        private Entry[] _entries;

        [DebuggerDisplay("({key}, {value})->{next}")]
        private struct Entry
        {
            public string key;
            public TValue value;
            // 0-based index of next entry in chain: -1 means end of chain
            public int next;
        }

        public StringDictionarySlim()
        {
            _buckets = HashHelpers.DictionarySlimSizeOneIntArray;
            _entries = InitialEntries;
        }

        public StringDictionarySlim(int capacity)
        {
            if (capacity < 2) ExceptionHelper.ThrowArgumentOutOfRangeException();
            capacity = HashHelpers.PowerOf2(capacity);
            _buckets = new int[capacity];
            _entries = new Entry[capacity];
        }

        public int Count => _count;

        public int Capacity => _entries.Length;

        public bool ContainsKey(string key)
        {
            Entry[] entries = _entries;
            for (int i = _buckets[key.GetHashCode() & (_buckets.Length - 1)] - 1;
                    (uint)i < (uint)entries.Length; i = entries[i].next)
            {
                if (key == entries[i].key)
                    return true;
            }

            return false;
        }

        public TValue GetValueOrDefault(string key)
        {
            bool result = TryGetValue(key, out TValue value);
            return value;
        }

        public bool TryGetValue(string key, out TValue value)
        {
            Entry[] entries = _entries;
            for (int i = _buckets[key.GetHashCode() & (_buckets.Length - 1)] - 1;
                    (uint)i < (uint)entries.Length; i = entries[i].next)
            {
                if (key == entries[i].key)
                {
                    value = entries[i].value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public bool Remove(string key)
        {
            Entry[] entries = _entries;
            int bucketIndex = key.GetHashCode() & (_buckets.Length - 1);
            int entryIndex = _buckets[bucketIndex] - 1;

            int lastIndex = -1;
            while (entryIndex != -1)
            {
                Entry candidate = entries[entryIndex];
                if (candidate.key == key)
                {
                    if (lastIndex == -1)
                    {
                        // Fixup bucket to new head (if any)
                        _buckets[bucketIndex] = candidate.next + 1;
                    }
                    else
                    {
                        // Fixup preceding element in chain to point to next (if any)
                        entries[lastIndex].next = candidate.next;
                    }

                    // move last item to this index and fix link to it
                    if (entryIndex != --_count)
                    {
                        entries[entryIndex] = entries[_count];

                        bucketIndex = entries[entryIndex].key.GetHashCode() & (_buckets.Length - 1);
                        lastIndex = _buckets[bucketIndex] - 1;

                        if (lastIndex == _count)
                        {
                            // Fixup bucket to this index
                            _buckets[bucketIndex] = entryIndex + 1;
                        }
                        else
                        {
                            // Find preceding element in chain and point to this index
                            while (entries[lastIndex].next != _count)
                                lastIndex = entries[lastIndex].next;
                            entries[lastIndex].next = entryIndex;
                        }
                    }
                    entries[_count] = default;
                    return true;
                }
                lastIndex = entryIndex;
                entryIndex = candidate.next;
            }

            return false;
        }

        public void Clear()
        {
            int count = _count;
            if (count > 0)
            {
                Array.Clear(_buckets, 0, _buckets.Length);
                _count = 0;
                Array.Clear(_entries, 0, count);
            }
        }

        public ref TValue this[string key]
        {
            get
            {
                Entry[] entries = _entries;
                int bucketIndex = key.GetHashCode() & (_buckets.Length - 1);
                for (int i = _buckets[bucketIndex] - 1;
                        (uint)i < (uint)entries.Length; i = entries[i].next)
                {
                    if (key == entries[i].key)
                        return ref entries[i].value;
                }

                return ref AddKey(key, bucketIndex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private ref TValue AddKey(string key, int bucketIndex)
        {
            Entry[] entries = _entries;

            if (_count == entries.Length || entries.Length == 1)
            {
                entries = Resize();
                bucketIndex = key.GetHashCode() & (_buckets.Length - 1);
                // entry indexes were not changed by Resize
            }

            int entryIndex = _count++;
            entries[entryIndex].key = key;
            entries[entryIndex].next = _buckets[bucketIndex] - 1;
            _buckets[bucketIndex] = entryIndex + 1;
            return ref entries[entryIndex].value;
        }

        private Entry[] Resize()
        {
            int count = _count;
            var entries = new Entry[_entries.Length * 2];
            Array.Copy(_entries, 0, entries, 0, count);

            var newBuckets = new int[entries.Length];
            while (count-- > 0)
            {
                int bucketIndex = entries[count].key.GetHashCode() & (newBuckets.Length - 1);
                entries[count].next = newBuckets[bucketIndex] - 1;
                newBuckets[bucketIndex] = count + 1;
            }

            _buckets = newBuckets;
            _entries = entries;

            return entries;
        }

        public KeyCollection Keys => new KeyCollection(this);

        public ValueCollection Values => new ValueCollection(this);

        public void CopyTo(KeyValuePair<string, TValue>[] array, int index)
        {
            Entry[] entries = _entries;
            for (int i = 0; i < _count; i++)
            {
                array[index++] = new KeyValuePair<string, TValue>(
                    entries[i].key,
                    entries[i].value);
            }
        }

        public Enumerator GetEnumerator() => new Enumerator(this); // avoid boxing

        public struct Enumerator : IEnumerator<KeyValuePair<string, TValue>>
        {
            private readonly StringDictionarySlim<TValue> _dictionary;
            private int _index;
            private KeyValuePair<string, TValue> _current;

            internal Enumerator(StringDictionarySlim<TValue> dictionary)
            {
                _dictionary = dictionary;
                _index = 0;
                _current = default;
            }

            public bool MoveNext()
            {
                if (_index == _dictionary._count)
                {
                    _current = default;
                    return false;
                }

                _current = new KeyValuePair<string, TValue>(
                    _dictionary._entries[_index].key,
                    _dictionary._entries[_index++].value);
                return true;
            }

            public KeyValuePair<string, TValue> Current => _current;

            object IEnumerator.Current => _current;

            void IEnumerator.Reset()
            {
                _index = 0;
            }

            public void Dispose() { }
        }

        public struct KeyCollection : ICollection<string>, IReadOnlyCollection<string>
        {
            private readonly StringDictionarySlim<TValue> _dictionary;

            internal KeyCollection(StringDictionarySlim<TValue> dictionary)
            {
                _dictionary = dictionary;
            }

            public int Count => _dictionary._count;

            bool ICollection<string>.IsReadOnly => true;

            void ICollection<string>.Add(string item) =>
                ExceptionHelper.ThrowNotSupportedException();

            void ICollection<string>.Clear() =>
                ExceptionHelper.ThrowNotSupportedException();

            public bool Contains(string item) => _dictionary.ContainsKey(item);

            bool ICollection<string>.Remove(string item) =>
                ExceptionHelper.ThrowNotSupportedException<bool>();

            public void CopyTo(string[] array, int index)
            {
                Entry[] entries = _dictionary._entries;
                for (int i = 0; i < _dictionary._count; i++)
                {
                    array[index++] = entries[i].key;
                }
            }

            public Enumerator GetEnumerator() => new Enumerator(_dictionary); // avoid boxing
            IEnumerator<string> IEnumerable<string>.GetEnumerator() => new Enumerator(_dictionary);
            IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dictionary);

            public struct Enumerator : IEnumerator<string>
            {
                private readonly StringDictionarySlim<TValue> _dictionary;
                private int _index;
                private string _current;

                internal Enumerator(StringDictionarySlim<TValue> dictionary)
                {
                    _dictionary = dictionary;
                    _index = 0;
                    _current = default;
                }

                public string Current => _current;

                object IEnumerator.Current => _current;

                public void Dispose() { }

                public bool MoveNext()
                {
                    if (_index == _dictionary._count)
                    {
                        _current = default;
                        return false;
                    }

                    _current = _dictionary._entries[_index++].key;
                    return true;
                }

                public void Reset()
                {
                    _index = 0;
                }
            }
        }

        public struct ValueCollection : ICollection<TValue>, IReadOnlyCollection<TValue>
        {
            private readonly StringDictionarySlim<TValue> _dictionary;

            internal ValueCollection(StringDictionarySlim<TValue> dictionary)
            {
                _dictionary = dictionary;
            }

            public int Count => _dictionary._count;

            bool ICollection<TValue>.IsReadOnly => true;

            void ICollection<TValue>.Add(TValue item) =>
                ExceptionHelper.ThrowNotSupportedException();

            void ICollection<TValue>.Clear() =>
                ExceptionHelper.ThrowNotSupportedException();

            bool ICollection<TValue>.Contains(TValue item) =>
                ExceptionHelper.ThrowNotSupportedException<bool>(); // performance antipattern

            bool ICollection<TValue>.Remove(TValue item) =>
                ExceptionHelper.ThrowNotSupportedException<bool>();

            public void CopyTo(TValue[] array, int index)
            {
                Entry[] entries = _dictionary._entries;
                for (int i = 0; i < _dictionary._count; i++)
                {
                    array[index++] = entries[i].value;
                }
            }

            public Enumerator GetEnumerator() => new Enumerator(_dictionary); // avoid boxing
            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => new Enumerator(_dictionary);
            IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dictionary);

            public struct Enumerator : IEnumerator<TValue>
            {
                private readonly StringDictionarySlim<TValue> _dictionary;
                private int _index;
                private TValue _current;

                internal Enumerator(StringDictionarySlim<TValue> dictionary)
                {
                    _dictionary = dictionary;
                    _index = 0;
                    _current = default;
                }

                public TValue Current => _current;

                object IEnumerator.Current => _current;

                public void Dispose() { }

                public bool MoveNext()
                {
                    if (_index == _dictionary._count)
                    {
                        _current = default;
                        return false;
                    }

                    _current = _dictionary._entries[_index++].value;
                    return true;
                }

                public void Reset()
                {
                    _index = 0;
                }
            }
        }

        internal static class HashHelpers
        {
            internal static readonly int[] DictionarySlimSizeOneIntArray = new int[1];

            internal static int PowerOf2(int v)
            {
                if ((v & (v - 1)) == 0) return v;
                int i = 2;
                while (i < v) i <<= 1;
                return i;
            }
        }

        internal sealed class DictionarySlimDebugView<V>
        {
            private readonly StringDictionarySlim<V> _dictionary;

            public DictionarySlimDebugView(StringDictionarySlim<V> dictionary)
            {
                _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public KeyValuePair<string, V>[] Items
            {
                get
                {
                    var array = new KeyValuePair<string, V>[_dictionary.Count];
                    _dictionary.CopyTo(array, 0);
                    return array;
                }
            }
        }
    }
}