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
        private static readonly ConcurrentObjectPool<StringBuilder> _pool;

        static StringBuilderPool()
        {
            _pool = new ConcurrentObjectPool<StringBuilder>(() => new StringBuilder());
        }

        public static BuilderWrapper Rent()
        {
            var builder = _pool.Allocate();
            Debug.Assert(builder.Length == 0);
            return new BuilderWrapper(builder, _pool);
        }

        internal readonly struct BuilderWrapper : IDisposable
        {
            public readonly StringBuilder Builder;
            private readonly ConcurrentObjectPool<StringBuilder> _pool;

            public BuilderWrapper(StringBuilder builder, ConcurrentObjectPool<StringBuilder> pool)
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
                    _pool.Free(builder);
                }
                else
                {
                    _pool.ForgetTrackedObject(builder);
                }
            }
        }
    }
}