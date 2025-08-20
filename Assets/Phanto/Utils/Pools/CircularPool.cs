// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace Phanto
{
    public class CircularPool<T> : Pool<T> where T : class
    {
        private int active;
        private int index;
        private readonly Dictionary<T, int> indices;

        private readonly Entry[] pool;

        public CircularPool(T primitive, int size, Callbacks callbacks)
        {
            pool = new Entry[size];
            indices = new Dictionary<T, int>(size);
            index = 0;
            active = 0;
            this.callbacks = callbacks;

            for (var i = 0; i < size; ++i)
            {
                var t = callbacks.Create(primitive);
                pool[i].t = t;
                indices[t] = i;
            }
        }

        public override int CountAll => pool.Length;
        public override int CountActive => active;

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
            if (index >= pool.Length) index = 0;

            var e = pool[index];
            if (e.active &&
                callbacks.OnRelease != null)
            {
                //alexdaws: If we are reusing an object that is currently in use,
                //           release it first before reusing it
                callbacks.OnRelease(e.t);
            }
            else
            {
                pool[index].active = true;
                ++active;
            }

            if (callbacks.OnGet != null) callbacks.OnGet(e.t);
            ++index;

            return e.t;
        }

        public override void Release(T t)
        {
            //alexdaws: protect against fragmentation from double releasing
            var eIndex = indices[t];
            if (pool[eIndex].active)
            {
                pool[eIndex].active = false;
                --active;

                //alexdaws: ensure that our released objects are first to be reused
                --index;
                if (index < 0) index = pool.Length - 1;
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

            active = 0;
        }

        private struct Entry
        {
            public bool active;
            public T t;
        }
    }
}
