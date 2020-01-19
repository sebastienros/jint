// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Jint.Collections.Maps
{
    internal abstract partial class Map<TValue>
    {
        // Instance with any number of key/value pairs.
        private sealed class ManyElementKeyedMap : Map<TValue>
        {
            private readonly Dictionary<Key, TValue> _dictionary;

            public override int Count => _dictionary.Count;

            public ManyElementKeyedMap(int capacity)
            {
                _dictionary = new Dictionary<Key, TValue>(capacity);
            }

            public override Map<TValue> Set(Key key, TValue value)
            {
                _dictionary[key] = value;
                return this;
            }

            public override Map<TValue> TryRemove(Key key, out bool success)
            {
                int count = _dictionary.Count;
                // If the key is contained in this map, we're going to create a new map that's one pair smaller.
                if (_dictionary.ContainsKey(key))
                {
                    // If the new count would be within range of a multi map instead of a many map,
                    // downgrade to the many map, which uses less memory and is faster to access.
                    // Otherwise, just create a new many map that's missing this key.
                    if (count == MultiElementKeyedMap.MaxMultiElements + 1)
                    {
                        var multi = new MultiElementKeyedMap(MultiElementKeyedMap.MaxMultiElements);
                        int index = 0;
                        foreach (KeyValuePair<Key, TValue> pair in _dictionary)
                        {
                            if (key != pair.Key)
                            {
                                multi.UnsafeStore(index++, pair.Key, pair.Value);
                            }
                        }
                        Debug.Assert(index == MultiElementKeyedMap.MaxMultiElements);
                        success = true;
                        return multi;
                    }
                    else
                    {
                        var map = new ManyElementKeyedMap(count - 1);
                        foreach (KeyValuePair<Key, TValue> pair in _dictionary)
                        {
                            if (key != pair.Key)
                            {
                                map[pair.Key] = pair.Value;
                            }
                        }
                        Debug.Assert(_dictionary.Count == count - 1);
                        success = true;
                        return map;
                    }
                }

                // The key wasn't in the map, so there's nothing to change.
                // Just return this instance.
                success = false;
                return this;
            }

            public override bool TryGetValue(Key key, out TValue value) => _dictionary.TryGetValue(key, out value);

            public TValue this[Key key]
            {
                get => _dictionary[key];
                set => _dictionary[key] = value;
            }

            public override bool TryGetNext(ref int index, out KeyValuePair<Key, TValue> value) => throw new NotSupportedException();

            public override DictionarySlim<TValue>.Enumerator GetEnumerator() => new DictionarySlim<TValue>.Enumerator(new ManyElementKeyedMapEnumerator(this));


            public override ICollection<Key> Keys => _dictionary.Keys;
            public override ICollection<TValue> Values => _dictionary.Values;

            private class ManyElementKeyedMapEnumerator : Map<TValue>
            {
                private Dictionary<Key, TValue>.Enumerator _enumerator;

                public ManyElementKeyedMapEnumerator(ManyElementKeyedMap map)
                {
                    _enumerator = map._dictionary.GetEnumerator();
                }

                public override bool TryGetNext(ref int index, out KeyValuePair<Key, TValue> value)
                {
                    var success = _enumerator.MoveNext();
                    value = success ? _enumerator.Current : default;

                    return success;
                }

                public override int Count => throw new NotSupportedException();
                public override Map<TValue> Set(Key key, TValue value) => throw new NotSupportedException();
                public override bool TryGetValue(Key key, out TValue value) => throw new NotSupportedException();
                public override Map<TValue> TryRemove(Key key, out bool success) => throw new NotSupportedException();
                public override ICollection<Key> Keys => throw new NotSupportedException();
                public override ICollection<TValue> Values => throw new NotSupportedException();
            }
        }
    }
}
