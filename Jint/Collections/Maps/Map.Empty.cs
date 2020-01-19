// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Jint.Collections.Maps
{
    internal abstract partial class Map<TValue>
    {
        // Instance without any key/value pairs. Used as a singleton.
        private sealed class EmptyMap : Map<TValue>
        {
            public override int Count => 0;

            public override Map<TValue> Set(Key key, TValue value)
            {
                // Create a new one-element map to store the key/value pair
                return new OneElementKeyedMap(key, value);
            }

            public override bool TryGetValue(Key key, out TValue value)
            {
                // Nothing here
                value = default;
                return false;
            }

            public override Map<TValue> TryRemove(Key key, out bool success)
            {
                // Nothing to remove
                success = false;
                return this;
            }

            public override bool TryGetNext(ref int index, out KeyValuePair<Key, TValue> value)
            {
                value = default;
                return false;
            }

            public override ICollection<Key> Keys => new Key[0];

            public override ICollection<TValue> Values => new TValue[0];
        }
    }
}
