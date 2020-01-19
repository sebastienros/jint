// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Jint.Collections.Maps;

namespace Jint.Collections
{
    internal partial class DictionarySlim<TValue>
    {
        public struct Enumerator : IEnumerator<KeyValuePair<Key, TValue>>
        {
            private readonly Map<TValue> _map;
            private int index;
            private KeyValuePair<Key, TValue> _current;

            internal Enumerator(Map<TValue> map)
            {
                _map = map;
                index = -1;
                _current = default;
            }

            public KeyValuePair<Key, TValue> Current => _current;

            public bool MoveNext() => _map.TryGetNext(ref index, out _current);

            public void Dispose()
            {
            }

            object IEnumerator.Current => _current;

            void IEnumerator.Reset() => throw new NotSupportedException();
        }
    }
}