// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Jint.Collections.Maps
{
    internal abstract partial class Map<TValue>
    {
        // Instance with two key/value pairs.
        private sealed class TwoElementKeyedMap : Map<TValue>
        {
            private readonly Key _key1, _key2;
            private TValue _value1, _value2;

            public override int Count => 2;

            public TwoElementKeyedMap(Key key1, TValue value1, Key key2, TValue value2)
            {
                _key1 = key1; _value1 = value1;
                _key2 = key2; _value2 = value2;
            }

            public override Map<TValue> Set(Key key, TValue value)
            {
                // If the key matches one already contained in this map  then update the value
                if (key == _key1)
                {
                    _value1 = value;
                }
                else if (key == _key2)
                {
                    _value2 = value;
                }
                else
                {
                    // Otherwise create a three-element map with the additional key/value.
                    return new ThreeElementKeyedMap(_key1, _value1, _key2, _value2, key, value);
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
                else
                {
                    value = default;
                    return false;
                }
            }

            public override Map<TValue> TryRemove(Key key, out bool success)
            {
                // If the key exists in this map, remove it by downgrading to a one-element map without the key.
                if (key == _key1)
                {
                    success = true;
                    return new OneElementKeyedMap(_key2, _value2);
                }
                else if (key == _key2)
                {
                    success = true;
                    return new OneElementKeyedMap(_key1, _value1);
                }
                else
                {
                    // Otherwise, there's nothing to add or remove, so just return this map.
                    success = false;
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
                else
                {
                    value = default;
                    return false;
                }

                return true;
            }

            public override ICollection<Key> Keys => new[] { _key1, _key2 };
            public override ICollection<TValue> Values => new[] { _value1, _value2 };
        }
    }
}
