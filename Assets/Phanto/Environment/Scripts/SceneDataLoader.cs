// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Meta.XR.MRUtilityKit;
using OVRSimpleJSON;
using PhantoUtils;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Phantom.Environment.Scripts
{
    // This class loads scene data from either Scene API or a JSON file.
    public class SceneDataLoader : MonoBehaviour
    {
        public enum SceneDataSource
        {
            SceneApi,
            StaticMeshDataPrefab,
            StaticMeshDataJson
        }

        // A reference to the MRUK prefab.
        [SerializeField] private MRUK MRUKPrefab;

        // The root transform of the MRUK.
        [SerializeField] private Transform sceneRoot;

        [SerializeField] private AnchorPrefabSpawner anchorPrefavSpawner;

        [SerializeField] private SceneDataLoaderSettings settings;

        [SerializeField] private bool loadAllRooms = false;

        // UnityEvent fired when scene data is loaded.
        public UnityEvent<Transform> SceneDataLoaded = new();

        // UnityEvent fired when scene data is not available.
        public UnityEvent NoSceneModelAvailable = new();

        // UnityEvent fired when a new scene model is available.
        public UnityEvent NewSceneModelAvailable = new();

        private MRUK _mruk;
        private Transform _staticMesh;

        private void Awake()
        {
            Assert.IsNotNull(MRUKPrefab, $"{nameof(MRUKPrefab)} cannot be null.");
            Assert.IsNotNull(sceneRoot, $"{nameof(sceneRoot)} cannot be null.");
        }

        private IEnumerator Start()
        {
            yield return null;
            LoadMeshes();
        }

        private void OnDestroy()
        {
            if (_mruk != null) _mruk.SceneLoadedEvent.RemoveListener(OnSceneAPIDataLoaded);
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
                StartCoroutine(LoadSceneAPIData());
            }
        }

        /// <summary>
        /// Load the scene model from the headset
        /// </summary>
        private IEnumerator LoadSceneAPIData()
        {
            Debug.Log($"{Application.productName}: Loading scene model.");

            if (_mruk == null)
            {
                // Scene Manager from previous scene.
                var existingManager = FindFirstObjectByType<MRUK>();

                if (existingManager != null) DestroyImmediate(existingManager.gameObject);

                _mruk = Instantiate(MRUKPrefab, transform);
                anchorPrefavSpawner = _mruk.GetComponent<AnchorPrefabSpawner>();
                Assert.IsNotNull(anchorPrefavSpawner, $"{nameof(anchorPrefavSpawner)} cannot be null.");
                anchorPrefavSpawner.SpawnOnStart =
                    loadAllRooms ? MRUK.RoomFilter.AllRooms : MRUK.RoomFilter.CurrentRoomOnly;
            }

            _mruk.RoomCreatedEvent.AddListener((room =>
            {
                Debug.Log($"{Application.productName}: {nameof(SceneDataLoader)}: RoomCreatedEvent ");
                // Set the initial room root.
                room.transform.parent = sceneRoot;
                anchorPrefavSpawner.onPrefabSpawned.AddListener(() =>
                {
                    StartCoroutine(WaitForPrefabSpawned());
                });
            }));

            // Wait for the manager to fully load the scene so we can get its dimensions and create
            _mruk.SceneLoadedEvent.AddListener((() =>
            {
                Debug.Log($"{Application.productName}: {nameof(SceneDataLoader)}: SceneModelLoadedSuccessfully ");
                OnSceneAPIDataLoaded();
            }));

            switch (settings.SceneDataSource)
            {
                // Scene API data.
                case SceneDataSource.SceneApi:
                    _mruk.SceneSettings.DataSource = MRUK.SceneDataSource.DeviceWithJsonFallback;
                    _mruk.SceneSettings.LoadSceneOnStartup = true;
                    _mruk.LoadSceneFromDevice();
                    break;
                // Static mesh data.
                case SceneDataSource.StaticMeshDataPrefab:
                    _mruk.SceneSettings.DataSource = MRUK.SceneDataSource.Prefab;
                    _mruk.LoadSceneFromPrefab(_mruk.SceneSettings.RoomPrefabs[0]);
                    // LoadStaticMesh(settings.SceneJson);
                    break;
                case SceneDataSource.StaticMeshDataJson:
                    _mruk.SceneSettings.DataSource = MRUK.SceneDataSource.Json;
                    _mruk.LoadSceneFromJsonString(_mruk.SceneSettings.SceneJsons[0].ToString());
                    // LoadStaticMesh(settings.SceneJson);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            yield return null;
        }

        public IEnumerator WaitForPrefabSpawned()
        {
            this.gameObject.SetLayerRecursively(LayerMask.NameToLayer("GlobalMesh"));
            this.gameObject.SetTagRecursively("GlobalMesh");
            yield return null;
        }



        /// <summary>
        /// Rescan the scene to update the list of available meshes.
        /// </summary>
        public void Rescan()
        {
            OVRScene.RequestSpaceSetup();
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
                    if (mf.TryGetComponent<MRUKAnchor>(out var semanticClassification)
                        && semanticClassification.Contains(MRUKAnchor.SceneLabels.GLOBAL_MESH.ToString()))
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

        public static void AddAnchorReferenceCount(MRUKAnchor anchor)
        {
            var anchorReferenceCountDictionary = typeof(MRUKAnchor).GetField("AnchorReferenceCountDictionary",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (anchorReferenceCountDictionary != null)
            {
                var refCountStaticField = anchorReferenceCountDictionary.GetValue(null);

                if (refCountStaticField is Dictionary<OVRSpace, int> refCountDictionary)
                {
                    // refCountDictionary[anchor.Space] = 1;

                    anchorReferenceCountDictionary.SetValue(null, refCountDictionary);
                }
            }
        }

        // Get Scene Mesh prefab override from AnchorPrefavSpawner
        public GameObject GetSceneMeshPrefab()
        {
            var meshes = anchorPrefavSpawner.PrefabsToSpawn.Select((group => group))
                .Where(group => group.Labels == MRUKAnchor.SceneLabels.GLOBAL_MESH).ToList();
            if (meshes.Count == 0)
                return null;

            return meshes.First().Prefabs.FirstOrDefault();
        }
    }
}
