// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace PhantoUtils
{
    public static class SingletonMonoBehaviour
    {
        public class InstantiationSettings : Attribute
        {
            public bool dontDestroyOnLoad;
        }
    }

    /// <summary>
    ///     A base class for creating singleton MonoBehaviours that can be instantiated in code or as part of the scene.<br />
    ///     <br />
    ///     If unspecified, SingletonMonoBehaviours <b>are</b> destroyed when a scene change occurs.<br />
    ///     Use the <see cref="SingletonMonoBehaviour.InstantiationSettings">InstantiationSettings</see> attribute to change
    ///     the DontDestroyOnLoad behaviour.
    /// </summary>
    /// <remarks>Consider setting [DefaultExecutionOrder(-1)] on the derived class.</remarks>
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null && Application.isPlaying)
                {
                    var existingInstances = FindObjectsOfType<T>(true);

                    // We don't handle multiple singletons in the scene, make the user clean it up
                    Assert.IsFalse(existingInstances.Length > 1,
                        $"There are {existingInstances.Length} instances of {typeof(T)} in the scene. Only one instance may exist.");

                    if (existingInstances.Length > 0) _instance = existingInstances[0];
                }

                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                InitializeSingleton();
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"An instance of {typeof(T)} already exists, destroying this instance.");
                Destroy(this);
            }
        }

        protected virtual void OnDestroy()
        {
            _instance = null;
        }

        private static void InitializeSingleton()
        {
            var attribute =
                Attribute.GetCustomAttribute(typeof(T), typeof(SingletonMonoBehaviour.InstantiationSettings));
            if (attribute is SingletonMonoBehaviour.InstantiationSettings instantiationSettings)
                if (instantiationSettings.dontDestroyOnLoad)
                    DontDestroyOnLoad(_instance.transform);
        }
    }
}
