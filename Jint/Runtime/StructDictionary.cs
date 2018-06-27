using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Jint.Runtime
{
    internal enum InsertionBehavior : byte
    {
        /// <summary>
        /// Specifies that an existing entry with the same key should be overwritten if encountered.
        /// </summary>
        OverwriteExisting = 1,

        /// <summary>
        /// Specifies that if an existing entry with the same key is encountered, an exception should be thrown.
        /// </summary>
        ThrowOnExisting = 2,
        
        /// <summary>
        /// Specifies that if existing entry with the same key is encountered, the update will be skipped.
        /// </summary>
        SkipIfExists = 3
    }

    /// <summary>
    /// Taken from .NET source to create performant specialized dictionary containing structs for Jint.
    /// </summary>
    internal sealed class StructDictionary<TValue> where TValue : struct
    {
        private static readonly EqualityComparer<string> _comparer; 
        
        static StructDictionary()
        {
            // we want to use same comparer as default dictionary impl that is hidden from us
            // .NET Core uses non-randomized hash code generation that is faster than default
            try
            {
                Dictionary<string, TValue> dictionary = new Dictionary<string, TValue>();
                var field = dictionary.GetType().GetField("_comparer", BindingFlags.Instance | BindingFlags.NonPublic);
                field = field ?? dictionary.GetType().GetField("comparer", BindingFlags.Instance | BindingFlags.NonPublic);
                _comparer = field?.GetValue(dictionary) as EqualityComparer<string> ?? EqualityComparer<string>.Default;
            }
            catch
            {
                _comparer = EqualityComparer<string>.Default;
            }
        }
        
        private struct Entry
        {
            public int hashCode; // Lower 31 bits of hash code, -1 if unused
            public int next; // Index of next entry, -1 if last
            public string key; // Key of entry
            public TValue value; // Value of entry
        }

        private int[] _buckets;
        private Entry[] _entries;
        private int _count;
        private int _freeList;
        private int _freeCount;
        private KeyCollection _keys;
        private ValueCollection _values;

        public StructDictionary() : this(0)
        {
        }

        public StructDictionary(int capacity)
        {
            if (capacity > 0) Initialize(capacity);
        }
                                
        public int Count => _count - _freeCount;

        public KeyCollection Keys
        {
            get
            {
                if (_keys == null) _keys = new KeyCollection(this);
                return _keys;
            }
        }

        public ValueCollection Values
        {
            get
            {
                if (_values == null) _values = new ValueCollection(this);
                return _values;
            }
        }

        public ref TValue GetItem(string key)
        {
            int i = FindEntry(key);
            if (i >= 0) return ref _entries[i].value;
            ExceptionHelper.ThrowArgumentException("key " + key + " not part of dictionary");
            return ref _entries[0].value;
        }

        public void Clear()
        {
            int count = _count;
            if (count > 0)
            {
                Array.Clear(_buckets, 0, _buckets.Length);

                _count = 0;
                _freeList = -1;
                _freeCount = 0;
                Array.Clear(_entries, 0, count);
            }
        }

        public bool ContainsKey(string key)
            => FindEntry(key) >= 0;

        public Enumerator GetEnumerator()
            => new Enumerator(this, Enumerator.KeyValuePair);

        private int FindEntry(string key)
        {
            int i = -1;
            int[] buckets = _buckets;
            Entry[] entries = _entries;
            int collisionCount = 0;
            if (buckets != null)
            {
                int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
                // Value in _buckets is 1-based
                i = buckets[hashCode % buckets.Length] - 1;
                do
                {
                    // Should be a while loop https://github.com/dotnet/coreclr/issues/15476
                    // Test in if to drop range check for following array access
                    if ((uint) i >= (uint) entries.Length ||
                        (entries[i].hashCode == hashCode && entries[i].key == key))
                    {
                        break;
                    }

                    i = entries[i].next;
                    if (collisionCount >= entries.Length)
                    {
                        // The chain of entries forms a loop; which means a concurrent update has happened.
                        // Break out of the loop and throw, rather than looping forever.
                        ExceptionHelper.ThrowInvalidOperationException();
                    }

                    collisionCount++;
                } while (true);
            }

            return i;
        }

        private int Initialize(int capacity)
        {
            int size = HashHelpers.GetPrime(capacity);

            _freeList = -1;
            _buckets = new int[size];
            _entries = new Entry[size];

            return size;
        }

        public bool TryInsert(string key, in TValue value, InsertionBehavior behavior)
        {
            if (_buckets == null)
            {
                Initialize(0);
            }

            Entry[] entries = _entries;

            int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;

            int collisionCount = 0;
            ref int bucket = ref _buckets[hashCode % _buckets.Length];
            // Value in _buckets is 1-based
            int i = bucket - 1;
            do
            {
                // Should be a while loop https://github.com/dotnet/coreclr/issues/15476
                // Test uint in if rather than loop condition to drop range check for following array access
                if ((uint) i >= (uint) entries.Length)
                {
                    break;
                }

                if (entries[i].hashCode == hashCode && entries[i].key == key)
                {
                    if (behavior == InsertionBehavior.OverwriteExisting)
                    {
                        entries[i].value = value;
                        return true;
                    }

                    if (behavior == InsertionBehavior.SkipIfExists)
                    {
                        return true;
                    }

                    if (behavior == InsertionBehavior.ThrowOnExisting)
                    {
                        ExceptionHelper.ThrowArgumentException("key already exists");
                    }

                    return false;
                }

                i = entries[i].next;
                if (collisionCount >= entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ExceptionHelper.ThrowInvalidOperationException();
                }

                collisionCount++;
            } while (true);

            bool updateFreeList = false;
            int index;
            if (_freeCount > 0)
            {
                index = _freeList;
                updateFreeList = true;
                _freeCount--;
            }
            else
            {
                int count = _count;
                if (count == entries.Length)
                {
                    Resize();
                    bucket = ref _buckets[hashCode % _buckets.Length];
                }

                index = count;
                _count = count + 1;
                entries = _entries;
            }

            ref Entry entry = ref entries[index];

            if (updateFreeList)
            {
                _freeList = entry.next;
            }

            entry.hashCode = hashCode;
            // Value in _buckets is 1-based
            entry.next = bucket - 1;
            entry.key = key;
            entry.value = value;
            // Value in _buckets is 1-based
            bucket = index + 1;

            return true;
        }

        private void Resize()
            => Resize(HashHelpers.ExpandPrime(_count), false);

        private void Resize(int newSize, bool forceNewHashCodes)
        {
            // Value types never rehash
            Debug.Assert(!forceNewHashCodes || default(string) == null);
            Debug.Assert(newSize >= _entries.Length);

            int[] buckets = new int[newSize];
            Entry[] entries = new Entry[newSize];

            int count = _count;
            Array.Copy(_entries, 0, entries, 0, count);

            if (forceNewHashCodes)
            {
                for (int i = 0; i < count; i++)
                {
                    if (entries[i].hashCode >= 0)
                    {
                        entries[i].hashCode = (_comparer.GetHashCode(entries[i].key) & 0x7FFFFFFF);
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (entries[i].hashCode >= 0)
                {
                    int bucket = entries[i].hashCode % newSize;
                    // Value in _buckets is 1-based
                    entries[i].next = buckets[bucket] - 1;
                    // Value in _buckets is 1-based
                    buckets[bucket] = i + 1;
                }
            }

            _buckets = buckets;
            _entries = entries;
        }

        // The overload Remove(string key, out TValue value) is a copy of this method with one additional
        // statement to copy the value for entry being removed into the output parameter.
        // Code has been intentionally duplicated for performance reasons.
        public bool Remove(string key)
        {
            int[] buckets = _buckets;
            Entry[] entries = _entries;
            int collisionCount = 0;
            if (buckets != null)
            {
                int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
                int bucket = hashCode % buckets.Length;
                int last = -1;
                // Value in buckets is 1-based
                int i = buckets[bucket] - 1;
                while (i >= 0)
                {
                    ref Entry entry = ref entries[i];

                    if (entry.hashCode == hashCode && entry.key == key)
                    {
                        if (last < 0)
                        {
                            // Value in buckets is 1-based
                            buckets[bucket] = entry.next + 1;
                        }
                        else
                        {
                            entries[last].next = entry.next;
                        }

                        entry.hashCode = -1;
                        entry.next = _freeList;
                        entry.key = null;
                        entry.value = default;

                        _freeList = i;
                        _freeCount++;
                        return true;
                    }

                    last = i;
                    i = entry.next;
                    if (collisionCount >= entries.Length)
                    {
                        // The chain of entries forms a loop; which means a concurrent update has happened.
                        // Break out of the loop and throw, rather than looping forever.
                        ExceptionHelper.ThrowInvalidOperationException();
                    }

                    collisionCount++;
                }
            }

            return false;
        }

        // This overload is a copy of the overload Remove(string key) with one additional
        // statement to copy the value for entry being removed into the output parameter.
        // Code has been intentionally duplicated for performance reasons.
        public bool Remove(string key, out TValue value)
        {
            int[] buckets = _buckets;
            Entry[] entries = _entries;
            int collisionCount = 0;
            if (buckets != null)
            {
                int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
                int bucket = hashCode % buckets.Length;
                int last = -1;
                // Value in buckets is 1-based
                int i = buckets[bucket] - 1;
                while (i >= 0)
                {
                    ref Entry entry = ref entries[i];

                    if (entry.hashCode == hashCode && entry.key == key)
                    {
                        if (last < 0)
                        {
                            // Value in buckets is 1-based
                            buckets[bucket] = entry.next + 1;
                        }
                        else
                        {
                            entries[last].next = entry.next;
                        }

                        value = entry.value;

                        entry.hashCode = -1;
                        entry.next = _freeList;
                        entry.key = null;
                        entry.value = default;

                        _freeList = i;
                        _freeCount++;
                        return true;
                    }

                    last = i;
                    i = entry.next;
                    if (collisionCount >= entries.Length)
                    {
                        // The chain of entries forms a loop; which means a concurrent update has happened.
                        // Break out of the loop and throw, rather than looping forever.
                        ExceptionHelper.ThrowInvalidOperationException();
                    }

                    collisionCount++;
                }
            }

            value = default;
            return false;
        }

        public bool TryGetValue(string key, out TValue value)
        {
            int i = FindEntry(key);
            if (i >= 0)
            {
                value = _entries[i].value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Ensures that the dictionary can hold up to 'capacity' entries without any further expansion of its backing storage
        /// </summary>
        public int EnsureCapacity(int capacity)
        {
            int currentCapacity = _entries == null ? 0 : _entries.Length;
            if (currentCapacity >= capacity)
                return currentCapacity;
            if (_buckets == null)
                return Initialize(capacity);
            int newSize = HashHelpers.GetPrime(capacity);
            Resize(newSize, forceNewHashCodes: false);
            return newSize;
        }
                                    
        public struct Enumerator : IEnumerator<KeyValuePair<string, TValue>>,
            IDictionaryEnumerator
        {
            private readonly StructDictionary<TValue> _dictionary;
            private int _index;
            private KeyValuePair<string, TValue> _current;
            private readonly int _getEnumeratorRetType; // What should Enumerator.Current return?

            internal const int DictEntry = 1;
            internal const int KeyValuePair = 2;

            internal Enumerator(StructDictionary<TValue> dictionary, int getEnumeratorRetType)
            {
                _dictionary = dictionary;
                _index = 0;
                _getEnumeratorRetType = getEnumeratorRetType;
                _current = new KeyValuePair<string, TValue>();
            }

            public bool MoveNext()
            {
                // Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
                // dictionary.count+1 could be negative if dictionary.count is int.MaxValue
                while ((uint) _index < (uint) _dictionary._count)
                {
                    ref Entry entry = ref _dictionary._entries[_index++];

                    if (entry.hashCode >= 0)
                    {
                        _current = new KeyValuePair<string, TValue>(entry.key, entry.value);
                        return true;
                    }
                }

                _index = _dictionary._count + 1;
                _current = new KeyValuePair<string, TValue>();
                return false;
            }

            public KeyValuePair<string, TValue> Current => _current;

            public void Dispose()
            {
            }

            object IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || (_index == _dictionary._count + 1))
                    {
                        ExceptionHelper.ThrowInvalidOperationException();
                    }

                    if (_getEnumeratorRetType == DictEntry)
                    {
                        return new DictionaryEntry(_current.Key, _current.Value);
                    }
                    else
                    {
                        return new KeyValuePair<string, TValue>(_current.Key, _current.Value);
                    }
                }
            }

            void IEnumerator.Reset()
            {
                _index = 0;
                _current = new KeyValuePair<string, TValue>();
            }

            DictionaryEntry IDictionaryEnumerator.Entry
            {
                get
                {
                    if (_index == 0 || (_index == _dictionary._count + 1))
                    {
                        ExceptionHelper.ThrowInvalidOperationException();
                    }

                    return new DictionaryEntry(_current.Key, _current.Value);
                }
            }

            object IDictionaryEnumerator.Key
            {
                get
                {
                    if (_index == 0 || (_index == _dictionary._count + 1))
                    {
                        ExceptionHelper.ThrowInvalidOperationException();
                    }

                    return _current.Key;
                }
            }

            object IDictionaryEnumerator.Value
            {
                get
                {
                    if (_index == 0 || (_index == _dictionary._count + 1))
                    {
                        ExceptionHelper.ThrowInvalidOperationException();
                    }

                    return _current.Value;
                }
            }
        }

        public sealed class KeyCollection
        {
            private readonly StructDictionary<TValue> _dictionary;

            public KeyCollection(StructDictionary<TValue> dictionary)
            {
                if (dictionary == null)
                {
                    ExceptionHelper.ThrowArgumentNullException(nameof(dictionary));
                }

                _dictionary = dictionary;
            }

            public Enumerator GetEnumerator()
                => new Enumerator(_dictionary);

            public int Count => _dictionary.Count;

            public struct Enumerator : IEnumerator<string>, IEnumerator
            {
                private readonly StructDictionary<TValue> _dictionary;
                private int _index;
                private string _currenstring;

                internal Enumerator(StructDictionary<TValue> dictionary)
                {
                    _dictionary = dictionary;
                    _index = 0;
                    _currenstring = default;
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    while ((uint) _index < (uint) _dictionary._count)
                    {
                        ref Entry entry = ref _dictionary._entries[_index++];

                        if (entry.hashCode >= 0)
                        {
                            _currenstring = entry.key;
                            return true;
                        }
                    }

                    _index = _dictionary._count + 1;
                    _currenstring = default;
                    return false;
                }

                public string Current => _currenstring;

                object IEnumerator.Current
                {
                    get
                    {
                        if (_index == 0 || (_index == _dictionary._count + 1))
                        {
                            ExceptionHelper.ThrowInvalidOperationException();
                        }

                        return _currenstring;
                    }
                }

                void IEnumerator.Reset()
                {
                    _index = 0;
                    _currenstring = default;
                }
            }
        }

        public sealed class ValueCollection
        {
            private readonly StructDictionary<TValue> _dictionary;

            public ValueCollection(StructDictionary<TValue> dictionary)
            {
                _dictionary = dictionary;
            }

            public Enumerator GetEnumerator()
                => new Enumerator(_dictionary);

            public int Count => _dictionary.Count;

            public struct Enumerator : IEnumerator<TValue>
            {
                private readonly StructDictionary<TValue> _dictionary;
                private int _index;
                private TValue _currentValue;

                internal Enumerator(StructDictionary<TValue> dictionary)
                {
                    _dictionary = dictionary;
                    _index = 0;
                    _currentValue = default;
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    while ((uint) _index < (uint) _dictionary._count)
                    {
                        ref Entry entry = ref _dictionary._entries[_index++];

                        if (entry.hashCode >= 0)
                        {
                            _currentValue = entry.value;
                            return true;
                        }
                    }

                    _index = _dictionary._count + 1;
                    _currentValue = default;
                    return false;
                }

                public TValue Current => _currentValue;

                object IEnumerator.Current
                {
                    get
                    {
                        if (_index == 0 || (_index == _dictionary._count + 1))
                        {
                            ExceptionHelper.ThrowInvalidOperationException();
                        }

                        return _currentValue;
                    }
                }

                void IEnumerator.Reset()
                {
                    _index = 0;
                    _currentValue = default;
                }
            }
        }

        private static class HashHelpers
        {
            public const int HashCollisionThreshold = 100;

            // This is the maximum prime smaller than Array.MaxArrayLength
            public const int MaxPrimeArrayLength = 0x7FEFFFFD;

            public const int HashPrime = 101;

            // Table of prime numbers to use as hash table sizes. 
            // A typical resize algorithm would pick the smallest prime number in this array
            // that is larger than twice the previous capacity. 
            // Suppose our Hashtable currently has capacity x and enough elements are added 
            // such that a resize needs to occur. Resizing first computes 2x then finds the 
            // first prime in the table greater than 2x, i.e. if primes are ordered 
            // p_1, p_2, ..., p_i, ..., it finds p_n such that p_n-1 < 2x < p_n. 
            // Doubling is important for preserving the asymptotic complexity of the 
            // hashtable operations such as add.  Having a prime guarantees that double 
            // hashing does not lead to infinite loops.  IE, your hash function will be 
            // h1(key) + i*h2(key), 0 <= i < size.  h2 and the size must be relatively prime.
            // We prefer the low computation costs of higher prime numbers over the increased
            // memory allocation of a fixed prime number i.e. when right sizing a HashSet.
            public static readonly int[] primes =
            {
                3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
                1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
                17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
                187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
                1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
            };

            public static bool IsPrime(int candidate)
            {
                if ((candidate & 1) != 0)
                {
                    int limit = (int) Math.Sqrt(candidate);
                    for (int divisor = 3; divisor <= limit; divisor += 2)
                    {
                        if ((candidate % divisor) == 0)
                            return false;
                    }

                    return true;
                }

                return (candidate == 2);
            }

            public static int GetPrime(int min)
            {
                if (min < 0)
                    throw new ArgumentException();

                for (int i = 0; i < primes.Length; i++)
                {
                    int prime = primes[i];
                    if (prime >= min)
                        return prime;
                }

                //outside of our predefined table. 
                //compute the hard way. 
                for (int i = (min | 1); i < int.MaxValue; i += 2)
                {
                    if (IsPrime(i) && ((i - 1) % HashPrime != 0))
                        return i;
                }

                return min;
            }

            // Returns size of hashtable to grow to.
            public static int ExpandPrime(int oldSize)
            {
                int newSize = 2 * oldSize;

                // Allow the hashtables to grow to maximum possible size (~2G elements) before encountering capacity overflow.
                // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
                if ((uint) newSize > MaxPrimeArrayLength && MaxPrimeArrayLength > oldSize)
                {
                    Debug.Assert(MaxPrimeArrayLength == GetPrime(MaxPrimeArrayLength), "Invalid MaxPrimeArrayLength");
                    return MaxPrimeArrayLength;
                }

                return GetPrime(newSize);
            }
        }
    }
}