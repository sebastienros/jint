// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Jint.Collections.Maps
{
    internal abstract partial class Map<TValue>
    {
        // Instance with three key/value pairs.
        private sealed class ThreeElementKeyedMap : Map<TValue>
        {
            private readonly Key _key1, _key2, _key3;
            private TValue _value1, _value2, _value3;

            public override int Count => 3;

            public ThreeElementKeyedMap(Key key1, TValue value1, Key key2, TValue value2, Key key3, TValue value3)
            {
                _key1 = key1; _value1 = value1;
                _key2 = key2; _value2 = value2;
                _key3 = key3; _value3 = value3;
            }

            public override Map<TValue> Set(Key key, TValue value)
            {
                // If the key matches one already contained in this map, then the update the value.
                if (key == _key1)
                {
                    _value1 = value;
                }
                else if (key == _key2)
                {
                    _value2 = value;
                }
                else if (key == _key3)
                {
                    _value3 = value;
                }
                else
                {
                    // The key doesn't exist in this map, so upgrade to a multi map that contains
                    // the additional key/value pair.
                    var multi = new MultiElementKeyedMap(4);
                    multi.UnsafeStore(0, _key1, _value1);
                    multi.UnsafeStore(1, _key2, _value2);
                    multi.UnsafeStore(2, _key3, _value3);
                    multi.UnsafeStore(3, key, value);
                    return multi;
                }

                return this;
            }

            public override bool TryGetValue(Key key, out TValue value)
            {
                if (key == _key1)
                {
                    value = _value1;
                    return true;
                }
                else if (key == _key2)
                {
                    value = _value2;
                    return true;
                }
                else if (key == _key3)
                {
                    value = _value3;
                    return true;
                }
                else
                {
                    value = default;
                    return false;
                }
            }

            public override Map<TValue> TryRemove(Key key, out bool success)
            {
                // If the key exists in this map, remove it by downgrading to a two - element map without the key.
                if (key == _key1)
                {
                    success = true;
                    return new TwoElementKeyedMap(_key2, _value2, _key3, _value3);
                }
                else if (key == _key2)
                {
                    success = true;
                    return new TwoElementKeyedMap(_key1, _value1, _key3, _value3);
                }
                else if (key == _key3)
                {
                    success = true;
                    return new TwoElementKeyedMap(_key1, _value1, _key2, _value2);
                }
                else
                {
                    // Otherwise, there's nothing to add or remove, so just return this map.
                    success = true;
                    return this;
                }
            }

            public override bool TryGetNext(ref int index, out KeyValuePair<Key, TValue> value)
            {
                index++;
                if (index == 0)
                {
                    value = new KeyValuePair<Key, TValue>(_key1, _value1);
                }
                else if (index == 1)
                {
                    value = new KeyValuePair<Key, TValue>(_key2, _value2);
                }
                else if (index == 1)
                {
                    value = new KeyValuePair<Key, TValue>(_key3, _value3);
                }
                else
                {
                    value = default;
                    return false;
                }

                return true;
            }

            public override ICollection<Key> Keys => new[] { _key1, _key2, _key3 };
            public override ICollection<TValue> Values => new[] { _value1, _value2, _value3 };
        }
    }
}
