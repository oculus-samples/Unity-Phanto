// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using UnityEngine;

namespace Phantom.Environment.Scripts
{
    /// <summary>
    /// The PhantoSceneMesh class stores information about a scene
    /// </summary>
    public class PhantoSceneMesh : MonoBehaviour
    {
        private static readonly Dictionary<Object, PhantoSceneMesh> meshCollection = new();

        [SerializeField] private new Collider collider;

        private void Awake()
        {
            meshCollection[collider] = this;
            meshCollection[transform] = this;
            meshCollection[gameObject] = this;
        }

        private void OnDestroy()
        {
            if (collider != null) meshCollection.Remove(collider);

            meshCollection.Remove(transform);
            meshCollection.Remove(gameObject);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (collider == null) collider = GetComponent<Collider>();
        }
#endif

        public static bool IsSceneMesh(Object other)
        {
            return meshCollection.ContainsKey(other);
        }

        public static bool TryGetSceneMesh(Object other, out PhantoSceneMesh sceneMesh)
        {
            return meshCollection.TryGetValue(other, out sceneMesh);
        }
    }
}
