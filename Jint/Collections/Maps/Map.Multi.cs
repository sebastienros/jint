// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Jint.Collections.Maps
{
    internal abstract partial class Map<TValue>
    {
        // Instance with up to 16 key/value pairs.
        private sealed class MultiElementKeyedMap : Map<TValue>
        {
            internal const int MaxMultiElements = 16;
            private readonly KeyValuePair<Key, TValue>[] _keyValues;

            public override int Count => _keyValues.Length;

            internal MultiElementKeyedMap(int count)
            {
                Debug.Assert(count <= MaxMultiElements);
                _keyValues = new KeyValuePair<Key, TValue>[count];
            }

            internal void UnsafeStore(int index, Key key, TValue value)
            {
                Debug.Assert(index < _keyValues.Length);
                _keyValues[index] = new KeyValuePair<Key, TValue>(key, value);
            }

            public override Map<TValue> Set(Key key, TValue value)
            {
                // Find the key in this map.
                var temp = _keyValues;
                for (uint i = 0; i < (uint) temp.Length; i++)
                {
                    if (key == temp[i].Key)
                    {
                        // The key is in the map. Update the value
                        temp[i] = new KeyValuePair<Key, TValue>(key, value);
                        return this;
                    }
                }

                // The key does not already exist in this map.

                // We need to create a new map that has the additional key/value pair.
                // If with the addition we can still fit in a multi map, create one.
                if (_keyValues.Length < MaxMultiElements)
                {
                    var multi = new MultiElementKeyedMap(_keyValues.Length + 1);
                    Array.Copy(_keyValues, 0, multi._keyValues, 0, _keyValues.Length);
                    multi._keyValues[_keyValues.Length] = new KeyValuePair<Key, TValue>(key, value);
                    return multi;
                }

                // Otherwise, upgrade to a many map.
                var many = new ManyElementKeyedMap(MaxMultiElements + 1);
                foreach (KeyValuePair<Key, TValue> pair in _keyValues)
                {
                    many[pair.Key] = pair.Value;
                }
                many[key] = value;
                return many;
            }

            public override bool TryGetValue(Key key, out TValue value)
            {
                foreach (KeyValuePair<Key, TValue> pair in _keyValues)
                {
                    if (key == pair.Key)
                    {
                        value = pair.Value;
                        return true;
                    }
                }

                value = default;
                return false;
            }

            public override Map<TValue> TryRemove(Key key, out bool success)
            {
                // Find the key in this map.
                for (int i = 0; i < _keyValues.Length; i++)
                {
                    if (key == _keyValues[i].Key)
                    {
                        // The key is in the map.  If the value isn't null, then create a new map of the same
                        // size that has all of the same pairs, with this new key/value pair overwriting the old.
                        if (_keyValues.Length == 4)
                        {
                            success = true;
                            // We only have four elements, one of which we're removing,
                            // so downgrade to a three-element map, without the matching element.
                            return
                                i == 0 ? new ThreeElementKeyedMap(_keyValues[1].Key, _keyValues[1].Value, _keyValues[2].Key, _keyValues[2].Value, _keyValues[3].Key, _keyValues[3].Value) :
                                i == 1 ? new ThreeElementKeyedMap(_keyValues[0].Key, _keyValues[0].Value, _keyValues[2].Key, _keyValues[2].Value, _keyValues[3].Key, _keyValues[3].Value) :
                                i == 2 ? new ThreeElementKeyedMap(_keyValues[0].Key, _keyValues[0].Value, _keyValues[1].Key, _keyValues[1].Value, _keyValues[3].Key, _keyValues[3].Value) :
                                         new ThreeElementKeyedMap(_keyValues[0].Key, _keyValues[0].Value, _keyValues[1].Key, _keyValues[1].Value, _keyValues[2].Key, _keyValues[2].Value);
                        }
                        else
                        {
                            success = true;
                            // We have enough elements remaining to warrant a multi map.
                            // Create a new one and copy all of the elements from this one, except the one to be removed.
                            var multi = new MultiElementKeyedMap(_keyValues.Length - 1);
                            if (i != 0) Array.Copy(_keyValues, 0, multi._keyValues, 0, i);
                            if (i != _keyValues.Length - 1) Array.Copy(_keyValues, i + 1, multi._keyValues, i, _keyValues.Length - i - 1);
                            return multi;
                        }
                    }
                }

                // Key not found, nothing to remove
                success = false;
                return this;
            }

            public override bool TryGetNext(ref int index, out KeyValuePair<Key, TValue> value)
            {
                index++;

                if ((uint)index >= (uint)_keyValues.Length)
                {
                    value = default;
                    return false;
                }

                value = _keyValues[index];
                return true;
            }

            public override ICollection<Key> Keys
            {
                get
                {
                    var keysValues = _keyValues;
                    var keys = new Key[keysValues.Length];
                    for (var i = 0; i < keysValues.Length; i++)
                    {
                        keys[i] = keysValues[i].Key;
                    }

                    return keys;
                }
            }

            public override ICollection<TValue> Values
            {
                get
                {
                    var keysValues = _keyValues;
                    var values = new TValue[keysValues.Length];
                    for (var i = 0; i < keysValues.Length; i++)
                    {
                        values[i] = keysValues[i].Value;
                    }

                    return values;
                }
            }
        }
    }
}
