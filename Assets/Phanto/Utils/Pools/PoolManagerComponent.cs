// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;
using Logger = PhantoUtils.Logger;

namespace Phanto
{
    public class PoolManagerComponent : MonoBehaviour
    {
        public enum Verbosity
        {
            NONE,
            PERFORMANCE
        }

        public static readonly Pool<GameObject>.Callbacks DEFAULT_CALLBACKS = new()
        {
            Create = DefaultCallbacks.Create,
            OnGet = DefaultCallbacks.OnGet,
            OnRelease = DefaultCallbacks.OnRelease,
            OnDestroy = DefaultCallbacks.OnDestroy
        };

        [SerializeField] private PoolDesc[] defaultPools;

        [SerializeField] private Verbosity verbosity;

        [NonSerialized] public PoolManager<GameObject, Pool<GameObject>> poolManager = new();

        private void Start()
        {
            InitDefaultPools();
        }

        private void InitDefaultPools()
        {
            foreach (var pd in defaultPools)
            {
                var callbacks = DEFAULT_CALLBACKS;
                var cp = pd.callbackProviderOverride == null
                    ? pd.primitive.GetComponent<CallbackProvider>()
                    : pd.callbackProviderOverride;
                if (cp != null) callbacks = cp.GetPoolCallbacks();

                Pool<GameObject> pool;
                switch (pd.poolType)
                {
                    case PoolDesc.PoolType.DYNAMIC:
                        pool = new DynamicPool<GameObject>(pd.primitive, pd.size, pd.size, callbacks);
                        break;
                    case PoolDesc.PoolType.FIXED:
                        pool = new FixedPool<GameObject>(pd.primitive, pd.size, callbacks);
                        break;
                    default:
                        pool = new CircularPool<GameObject>(pd.primitive, pd.size, callbacks);
                        break;
                }

                poolManager.AddPool(pd.primitive, pool);
            }
        }

        /*
         * Create is a drop-in replacement for Instantiate that uses a pool if available.
         * Note that it is not named Instantiate so that it is easy to find & replace Instantiate
         *  calls with Create calls when switching to using Pools.
         */
        public GameObject Create(GameObject primitive,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            var pool = poolManager.GetPool(primitive);
            if (pool == null)
            {
                if (verbosity == Verbosity.PERFORMANCE)
                    Logger.Log(Logger.Type.Performance, Logger.Severity.Severe,
                        "Could not find a pool for " + primitive.name + "!  Consider adding one to your default pools.",
                        this);

                return Instantiate(primitive,
                    position,
                    rotation,
                    parent);
            }

            //alexdaws: Temporarily disable the OnGet callback,
            //           as we only want to call it after we've adjusted the GameObject's transform
            var onGet = pool.callbacks.OnGet;
            pool.callbacks.OnGet = null;

            try
            {
                var go = pool.Get();
                if (go == null)
                    //alexdaws: if we are using a FixedPool, we have run out of pooled GameObjects
                    return null;
                Poolable poolable;
                if (!go.TryGetComponent(out poolable)) poolable = go.AddComponent<Poolable>();
                poolable.pool = pool;

                go.transform.SetParent(parent);
                go.transform.SetPositionAndRotation(position, rotation);

                onGet(go);
                return go;
            }
            finally
            {
                //alexdaws: ensure the OnGet callback gets restored
                pool.callbacks.OnGet = onGet;
            }
        }

        public GameObject Create(GameObject primitive,
            Transform parent = null,
            bool instantiateInWorldSpace = false)
        {
            var pool = poolManager.GetPool(primitive);
            if (pool == null)
            {
                if (verbosity == Verbosity.PERFORMANCE)
                    Logger.Log(Logger.Type.Performance, Logger.Severity.Severe,
                        "Could not find a pool for " + primitive.name + "!  Consider adding one to your default pools.",
                        this);

                return Instantiate(primitive,
                    parent,
                    instantiateInWorldSpace);
            }

            //alexdaws: Temporarily disable the OnGet callback,
            //           as we only want to call it after we've adjusted the GameObject's transform
            var onGet = pool.callbacks.OnGet;
            pool.callbacks.OnGet = null;

            try
            {
                var go = pool.Get();
                if (go == null)
                    //alexdaws: if we are using a FixedPool, we have run out of pooled GameObjects
                    return null;

                Poolable poolable;
                if (!go.TryGetComponent(out poolable)) poolable = go.AddComponent<Poolable>();
                poolable.pool = pool;

                go.transform.SetParent(parent);
                if (parent)
                {
                    if (instantiateInWorldSpace)
                    {
                        go.transform.SetPositionAndRotation(parent.position, parent.rotation);
                    }
                    else
                    {
                        //alexdaws: In 2023.1.0a21+, SetLocalPositionAndRotation() is more performant.
                        //          Assign properties directly for wider compatibility.
                        go.transform.localRotation = parent.localRotation;
                        go.transform.localPosition = parent.localPosition;
                    }
                }

                onGet(go);
                return go;
            }
            finally
            {
                //alexdaws: ensure the OnGet callback gets restored
                pool.callbacks.OnGet = onGet;
            }
        }

        public T Create<T>(T primitive,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null) where T : Component
        {
            var go = Create(primitive.gameObject,
                position,
                rotation,
                parent);
            return go == null ? null : go.GetComponent<T>();
        }

        public T Create<T>(T primitive,
            Transform parent = null,
            bool instantiateInWorldSpace = false) where T : Component
        {
            var go = Create(primitive.gameObject,
                parent,
                instantiateInWorldSpace);
            return go == null ? null : go.GetComponent<T>();
        }

        /*
         * Discard is a drop-in replacement for Destroy that releases into a pool if available.
         * Note that it is not named Destroy so that it is easy to find & replace Destroy
         *  calls with Discard calls when switching to using Pools.
         */
        public void Discard(GameObject go)
        {
            Poolable poolable;
            if (go.TryGetComponent(out poolable) &&
                poolable.pool != null)
                poolable.pool.Release(go);
            else
                Destroy(go);
        }

        [Serializable]
        public abstract class CallbackProvider : MonoBehaviour
        {
            public abstract Pool<GameObject>.Callbacks GetPoolCallbacks();
        }

        [Serializable]
        public class Poolable : MonoBehaviour
        {
            internal Pool<GameObject> pool;
        }

        [Serializable]
        private struct PoolDesc
        {
            public enum PoolType
            {
                CIRCULAR,
                FIXED,
                DYNAMIC
            }

            public PoolType poolType;
            public GameObject primitive;
            public int size;
            public CallbackProvider callbackProviderOverride;
        }

        private static class DefaultCallbacks
        {
            public static GameObject Create(GameObject primitive)
            {
                var e = primitive.activeSelf;
                primitive.SetActive(false);
                var go = Instantiate(primitive, Vector3.zero, Quaternion.identity);
                primitive.SetActive(e);
                return go;
            }

            public static void OnGet(GameObject go)
            {
                go.SetActive(true);
            }

            public static void OnRelease(GameObject go)
            {
                go.SetActive(false);
            }

            public static void OnDestroy(GameObject go)
            {
                Destroy(go);
            }
        }
    }
}
