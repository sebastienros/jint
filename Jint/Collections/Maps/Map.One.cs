// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Jint.Collections.Maps
{
    internal abstract partial class Map<TValue>
    {
        // Instance with one key/value pair.
        private sealed class OneElementKeyedMap : Map<TValue>
        {
            private readonly Key _key1;
            private TValue _value1;

            public override int Count => 1;

            public OneElementKeyedMap(Key key, TValue value)
            {
                _key1 = key; _value1 = value;
            }

            public override Map<TValue> Set(Key key, TValue value)
            {
                // If the key matches one already contained in this map update it,
                // otherwise create a two-element map with the additional key/value.
                if (key == _key1)
                {
                    _value1 = value;
                    return this;
                }
                else
                {
                    return new TwoElementKeyedMap(_key1, _value1, key, value);
                }
            }

            public override bool TryGetValue(Key key, out TValue value)
            {
                if ((key == _key1))
                {
                    value = _value1;
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
                success = (key == _key1);
                if (success)
                {
                    // Return the Empty singleton
                    return Empty;
                }
                return this;
            }

            public override bool TryGetNext(ref int index, out KeyValuePair<Key, TValue> value)
            {
                index++;
                if (index == 0)
                {
                    value = new KeyValuePair<Key, TValue>(_key1, _value1);
                    return true;
                }

                value = default;
                return false;
            }

            public override ICollection<Key> Keys => new[] { _key1 };
            public override ICollection<TValue> Values => new[] { _value1 };
        }
    }
}
