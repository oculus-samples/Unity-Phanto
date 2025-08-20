// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace Phanto
{
    public class PoolManager<K, P> where K : class
        where P : Pool<K>
    {
        private readonly Dictionary<K, P> pools = new();

        public void AddPool(K primitive, P pool)
        {
            pools.Add(primitive, pool);
        }

        public bool ContainsPool(K primitive)
        {
            return pools.ContainsKey(primitive);
        }

        public P GetPool(K primitive)
        {
            pools.TryGetValue(primitive, out var pool);
            return pool;
        }

        public void Clear()
        {
            foreach (var pool in pools.Values) pool.Clear();

            pools.Clear();
        }
    }
}
