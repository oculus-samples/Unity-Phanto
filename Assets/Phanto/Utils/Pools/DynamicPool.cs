// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine.Pool;

namespace Phanto
{
    public class DynamicPool<T> : Pool<T> where T : class
    {
        private readonly ObjectPool<T> pool;
        private readonly T primitive;

        public DynamicPool(T primitive, int capacity, int maxSize, Callbacks callbacks)
        {
            this.primitive = primitive;
            this.callbacks = callbacks;
            pool = new ObjectPool<T>(Create,
                OnGet,
                OnRelease,
                OnDestroy,
                false,
                capacity,
                maxSize);
        }

        public override int CountAll => pool.CountAll;
        public override int CountActive => pool.CountActive;
        public override int CountInactive => pool.CountInactive;

        private T Create()
        {
            return callbacks.Create(primitive);
        }

        private void OnGet(T t)
        {
            if (callbacks.OnGet != null) callbacks.OnGet(t);
        }

        private void OnRelease(T t)
        {
            if (callbacks.OnRelease != null) callbacks.OnRelease(t);
        }

        private void OnDestroy(T t)
        {
            if (callbacks.OnDestroy != null) callbacks.OnDestroy(t);
        }

        public override T Get()
        {
            return pool.Get();
        }

        public override void Release(T t)
        {
            pool.Release(t);
        }

        public override void Clear()
        {
            pool.Clear();
        }
    }
}
