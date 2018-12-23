// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text;

namespace Jint.Pooling
{
    /// <summary>
    /// The usage is:
    ///        var inst = PooledStringBuilder.GetInstance();
    ///        var sb = inst.builder;
    ///        ... Do Stuff...
    ///        ... sb.ToString() ...
    ///        inst.Free();
    /// </summary>
    internal sealed class StringBuilderPool : IDisposable
    {
        // global pool
        private static readonly ObjectPool<StringBuilderPool> s_poolInstance = CreatePool();

        public readonly StringBuilder Builder = new StringBuilder();
        private readonly ObjectPool<StringBuilderPool> _pool;

        private StringBuilderPool(ObjectPool<StringBuilderPool> pool)
        {
            Debug.Assert(pool != null);
            _pool = pool;
        }

        public int Length => Builder.Length;

        // if someone needs to create a private pool;
        /// <summary>
        /// If someone need to create a private pool
        /// </summary>
        /// <param name="size">The size of the pool.</param>
        /// <returns></returns>
        internal static ObjectPool<StringBuilderPool> CreatePool(int size = 32)
        {
            ObjectPool<StringBuilderPool> pool = null;
            pool = new ObjectPool<StringBuilderPool>(() => new StringBuilderPool(pool), size);
            return pool;
        }

        public static StringBuilderPool GetInstance()
        {
            var builder = s_poolInstance.Allocate();
            Debug.Assert(builder.Builder.Length == 0);
            return builder;
        }

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
                _pool.Free(this);
            }
            else
            {
                _pool.ForgetTrackedObject(this);
            }
        }
    }
}