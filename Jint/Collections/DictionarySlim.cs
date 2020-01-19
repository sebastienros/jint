// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Jint.Collections.Maps;

namespace Jint.Collections
{
    internal partial class DictionarySlim<TValue>
    {
        private Map<TValue> _map = Map<TValue>.Empty;

        public TValue this[Key key]
        {
            get => _map.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
            set => _map = _map.Set(key, value);
        }

        public ICollection<Key> Keys => _map.Keys;

        public ICollection<TValue> Values => _map.Values;

        public int Count => _map.Count;

        public bool IsReadOnly => false;

        public void Clear() => _map = Map<TValue>.Empty;

        public bool ContainsKey(Key key) => _map.TryGetValue(key, out _);

        public bool TryGetValue(Key key, out TValue value) => _map.TryGetValue(key, out value);

        public bool Remove(Key key)
        {
            _map = _map.TryRemove(key, out var success);
            return success;
        }

        public Enumerator GetEnumerator() => new Enumerator(_map);
    }
}