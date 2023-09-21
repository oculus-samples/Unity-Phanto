// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;

namespace Phanto
{
    public abstract class Pool<T> where T : class
    {
        public Callbacks callbacks;

        public abstract int CountAll { get; }
        public abstract int CountActive { get; }
        public virtual int CountInactive => CountAll - CountActive;

        public abstract T Get();
        public abstract void Release(T t);

        public abstract void Clear();

        public struct Callbacks
        {
            public Func<T, T> Create;
            public Action<T> OnGet, OnRelease, OnDestroy;
        }
    }
}
