// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace Phanto
{
    public class FixedPool<T> : Pool<T> where T : class
    {
        private int index;
        private readonly Dictionary<T, int> indices;

        private readonly Entry[] pool;

        public FixedPool(T primitive, int size, Callbacks callbacks)
        {
            pool = new Entry[size];
            indices = new Dictionary<T, int>(size);
            index = 0;
            this.callbacks = callbacks;

            for (var i = 0; i < size; ++i)
            {
                var t = callbacks.Create(primitive);
                pool[i].t = t;
                indices[t] = i;
            }
        }

        public override int CountAll => pool.Length;
        public override int CountActive => index;

        private void Swap(int i0, int i1)
        {
            indices[pool[i0].t] = i1;
            indices[pool[i1].t] = i0;

            var temp = pool[i0];
            pool[i0] = pool[i1];
            pool[i1] = temp;
        }

        public override T Get()
        {
            if (index >= pool.Length) return null;

            var t = pool[index].t;
            pool[index].active = true;
            if (callbacks.OnGet != null) callbacks.OnGet(t);
            ++index;

            return t;
        }

        public override void Release(T t)
        {
            //alexdaws: protect against double releasing
            var eIndex = indices[t];
            if (pool[eIndex].active)
            {
                pool[eIndex].active = false;

                --index;
                Swap(eIndex, index);
                if (callbacks.OnRelease != null) callbacks.OnRelease(t);
            }
        }

        public override void Clear()
        {
            if (callbacks.OnDestroy != null)
                for (var i = 0; i < pool.Length; ++i)
                {
                    pool[index].active = false;
                    callbacks.OnDestroy(pool[index].t);
                }

            index = 0;
        }

        private struct Entry
        {
            public bool active;
            public T t;
        }
    }
}
