// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Jint.Collections.Maps
{
    internal abstract partial class Map<TValue>
    {
        public static Map<TValue> Empty { get; } = new EmptyMap();

        public abstract int Count { get; }

        public abstract Map<TValue> Set(Key key, TValue value);

        public abstract Map<TValue> TryRemove(Key key, out bool success);

        public abstract bool TryGetValue(Key key, out TValue value);

        public virtual DictionarySlim<TValue>.Enumerator GetEnumerator() => new DictionarySlim<TValue>.Enumerator(this);

        public abstract bool TryGetNext(ref int index, out KeyValuePair<Key, TValue> value);

        public abstract ICollection<Key> Keys { get; }

        public abstract ICollection<TValue> Values { get; }
    }
}
