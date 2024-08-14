// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OVRSimpleJSON;
using PhantoUtils;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;
using Classification = OVRSceneManager.Classification;

namespace Phantom.Environment.Scripts
{
    // This class loads scene data from either Scene API or a JSON file.
    public class SceneDataLoader : MonoBehaviour
    {
        public enum SceneDataSource
        {
            SceneApi,
            StaticMeshData
        }

        // Scene API data
        private static readonly string[] MeshClassifications =
            { "GlobalMesh", Classification.GlobalMesh };

        // A reference to the OVRSceneManager prefab.
        [SerializeField] private OVRSceneManager ovrSceneManagerPrefab;

        // The root transform of the OVRSceneManager.
        [SerializeField] private Transform sceneRoot;

        [SerializeField] private SceneDataLoaderSettings settings;

        [SerializeField] private bool loadAllRooms = false;

        // UnityEvent fired when scene data is loaded.
        public UnityEvent<Transform> SceneDataLoaded = new();

        // UnityEvent fired when scene data is not available.
        public UnityEvent NoSceneModelAvailable = new();

        // UnityEvent fired when a new scene model is available.
        public UnityEvent NewSceneModelAvailable = new();

        private OVRSceneManager ovrSceneManager;
        private Transform staticMesh;

        private void Awake()
        {
            Assert.IsNotNull(ovrSceneManagerPrefab, $"{nameof(ovrSceneManagerPrefab)} cannot be null.");
            Assert.IsNotNull(sceneRoot, $"{nameof(sceneRoot)} cannot be null.");
        }

        private IEnumerator Start()
        {
            yield return null;
            LoadMeshes();
        }

        private void OnDestroy()
        {
            if (ovrSceneManager != null) ovrSceneManager.SceneModelLoadedSuccessfully -= OnSceneAPIDataLoaded;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (sceneRoot == null) sceneRoot = transform;
        }
#endif
        /// <summary>
        /// Loads the scene model from the headset or the provided JSON data.
        public void LoadMeshes()
        {
            if (settings.LoadSceneOnStart)
            {
                Debug.Log($"{Application.productName}: Loading scene.");
#if UNITY_EDITOR
                if (!OVRManager.isHmdPresent && !string.IsNullOrEmpty(settings.SceneJson))
                {
                    LoadStaticMesh(settings.SceneJson);
                    return;
                }
#endif

                switch (settings.SceneDataSource)
                {
                    // Scene API data.
                    case SceneDataSource.SceneApi:
                        StartCoroutine(LoadSceneAPIData());
                        break;
                    // Static mesh data.
                    case SceneDataSource.StaticMeshData:
                        LoadStaticMesh(settings.SceneJson);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Load the scene model from the headset
        /// </summary>
        private IEnumerator LoadSceneAPIData()
        {
            Debug.Log($"{Application.productName}: Loading scene model.");

            if (ovrSceneManager == null)
            {
                // Scene Manager from previous scene.
                var existingManager = FindObjectOfType<OVRSceneManager>();

                if (existingManager != null) DestroyImmediate(existingManager.gameObject);

                ovrSceneManager = Instantiate(ovrSceneManagerPrefab, transform);
                ovrSceneManager.ActiveRoomsOnly = !loadAllRooms;
            }

            // Set the initial room root.
            ovrSceneManager.InitialAnchorParent = sceneRoot;

            // Wait for the manager to fully load the scene so we can get its dimensions and create
            ovrSceneManager.SceneModelLoadedSuccessfully += () =>
            {
                Debug.Log($"{Application.productName}: {nameof(SceneDataLoader)}: SceneModelLoadedSuccessfully ");
                OnSceneAPIDataLoaded();
            };
            // Wait until the manager has completed one update to start the loading process.
            ovrSceneManager.SceneCaptureReturnedWithoutError += () =>
            {
                Debug.Log(
                    $"{Application.productName}: {nameof(SceneDataLoader)}: SceneCaptureReturnedWithoutError ");
            };
            // Catch the various errors that can occur when the scene capture is started.
            ovrSceneManager.UnexpectedErrorWithSceneCapture += () =>
            {
                Debug.LogError(
                    $"{Application.productName}: {nameof(SceneDataLoader)}: UnexpectedErrorWithSceneCapture ");
                NoSceneModelAvailable?.Invoke();
            };
            ovrSceneManager.NoSceneModelToLoad += () =>
            {
                Debug.LogError($"{Application.productName}: {nameof(SceneDataLoader)}: NoSceneModelToLoad ");
                NoSceneModelAvailable?.Invoke();
            };
            ovrSceneManager.NewSceneModelAvailable += () =>
            {
                Debug.Log($"{Application.productName}: {nameof(SceneDataLoader)}: NewSceneModelAvailable ");
                if (ovrSceneManager.LoadSceneModel())
                {
                    NewSceneModelAvailable?.Invoke();
                }
            };
            yield return null;
        }

        /// <summary>
        /// Rescan the scene to update the list of available meshes.
        /// </summary>
        public void Rescan()
        {
            ovrSceneManager.RequestSceneCapture();
        }

        /// <summary>
        /// Load a static mesh from a JSON string.
        /// </summary>
        private void LoadStaticMesh(string jsonText)
        {
            var json = JSON.Parse(jsonText);

            var instantiatedMesh = JsonSceneBuilder.SpawnSceneRoom(json, sceneRoot, ovrSceneManagerPrefab);

            if (settings.CenterStaticMesh)
                // Center mesh on tracking space.
                AlignStaticMesh(instantiatedMesh);
            SceneDataLoaded?.Invoke(sceneRoot);

            var debugSceneEntities = gameObject.AddComponent<DebugSceneEntities>();
            debugSceneEntities.StaticSceneModelLoaded();
        }

        /// <summary>
        ///     Shift static mesh so it's centered around the origin and floors match.
        /// </summary>
        /// <param name="root"></param>
        private void AlignStaticMesh(Transform root)
        {
            // FIXME: find the room the user is closest to and center that room around user.

            var meshFilters = root.GetComponentsInChildren<MeshFilter>();

            MeshFilter globalMesh = null;

            if (meshFilters.Length == 1)
                globalMesh = meshFilters[0];
            else
                foreach (var mf in meshFilters)
                {
                    if (mf.TryGetComponent<OVRSemanticClassification>(out var semanticClassification)
                        && semanticClassification.Contains(OVRSceneManager.Classification.GlobalMesh))
                    {
                        globalMesh = mf;
                        break;
                    }
                }

            if (globalMesh == null)
            {
                return;
            }

            var bounds = globalMesh.mesh.bounds;
            var meshTransform = globalMesh.transform;

            var worldMin = meshTransform.TransformPoint(bounds.min);
            var worldCenter = meshTransform.TransformPoint(bounds.center);
            worldCenter.y = worldMin.y;

            root.position -= worldCenter;
        }

        private void OnSceneAPIDataLoaded()
        {
            SceneDataLoaded?.Invoke(sceneRoot);
        }

        public OVRSceneAnchor GetSceneMeshPrefab()
        {
            foreach (var prefabOverride in ovrSceneManagerPrefab.PrefabOverrides)
            {
                if (prefabOverride.ClassificationLabel == OVRSceneManager.Classification.GlobalMesh)
                {
                    return prefabOverride.Prefab;
                }
            }

            return null;
        }

        /// <summary>
        ///     Brittle method for finding the global mesh in a prefab that contains multiple static meshes.
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        private static MeshFilter FindGlobalMeshFilter(Transform root)
        {
            var meshFilters = root.GetComponentsInChildren<MeshFilter>();

            if (meshFilters.Length == 1) return meshFilters[0];

            foreach (var mf in meshFilters)
            {
                var tf = mf.transform;
                while (tf != root)
                {
                    if (tf.name.Contains("GlobalMesh", StringComparison.InvariantCultureIgnoreCase)) return mf;
                    tf = tf.parent;
                }
            }

            return null;
        }

        public static void AddAnchorReferenceCount(OVRSceneAnchor anchor)
        {
            var anchorReferenceCountDictionary = typeof(OVRSceneAnchor).GetField("AnchorReferenceCountDictionary",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (anchorReferenceCountDictionary != null)
            {
                var refCountStaticField = anchorReferenceCountDictionary.GetValue(null);

                if (refCountStaticField is Dictionary<OVRSpace, int> refCountDictionary)
                {
                    refCountDictionary[anchor.Space] = 1;

                    anchorReferenceCountDictionary.SetValue(null, refCountDictionary);
                }
            }
        }
    }
}
