// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text;

namespace Jint.Pooling
{
    /// <summary>
    /// Pooling of StringBuilder instances.
    /// </summary>
    internal sealed class StringBuilderPool
    {
        private readonly ObjectPool<StringBuilder> _pool;

        public StringBuilderPool()
        {
            _pool = new ObjectPool<StringBuilder>(() => new StringBuilder());
        }

        public BuilderWrapper Rent()
        {
            var builder = _pool.Allocate();
            Debug.Assert(builder.Length == 0);
            return new BuilderWrapper(builder, this);
        }

        internal readonly struct BuilderWrapper : IDisposable
        {
            public readonly StringBuilder Builder;
            private readonly StringBuilderPool _pool;

            public BuilderWrapper(StringBuilder builder, StringBuilderPool pool)
            {
                Builder = builder;
                _pool = pool;
            }

            public int Length => Builder.Length;

            public override string ToString()
            {
                return Builder.ToString();
            }

            public void Dispose()
            {
                var builder = Builder;

                // do not store builders that are too large.
                if (builder.Capacity <= 1024)
                {
                    builder.Clear();
                    _pool._pool.Free(builder);
                }
                else
                {
                    _pool._pool.ForgetTrackedObject(builder);
                }
            }
        }
    }
}