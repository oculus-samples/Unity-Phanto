// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PhantoUtils;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;
using Classification = OVRSceneManager.Classification;

namespace Phantom.Environment.Scripts
{
    /// <summary>
    /// Post processes scene data after scene loading.
    /// </summary>
    public class PhantoScenePostProcessor : MonoBehaviour
    {
        // Items in the room probably large enough for phantoms to hop on
        private static readonly string[] WalkableFurniture =
        {
            Classification.Table, Classification.Couch,
            Classification.Other, Classification.Storage,
            Classification.Bed,
        };

        // Items in the room that phantoms shouldn't hop on
        private static readonly string[] RangedTargets =
        {
            Classification.Screen,
            Classification.Lamp,
            Classification.Plant,
        };

        // Items in the room that phantoms can spit goo at
        private static readonly string[] WallMountedTargets =
        {
            Classification.WallArt,
            Classification.WindowFrame,
            Classification.DoorFrame,
        };

        [SerializeField] private NavMeshGenerator navMeshGeneratorPrefab;
        [SerializeField] private FurnitureNavMeshGenerator furnitureNavMeshGeneratorPrefab;
        [SerializeField] private RangedFurnitureTarget rangedFurnitureTargetPrefab;

        [SerializeField] private UnityEvent<Transform> SceneDataProcessed = new();

        [SerializeField] private bool hideSceneMesh;
        [SerializeField] private bool generateNavMesh = true;
        private bool _boundsUpdated;

        private void OnEnable()
        {
            SceneBoundsChecker.BoundsChanged += OnBoundsChanged;
        }

        private void OnDisable()
        {
            SceneBoundsChecker.BoundsChanged -= OnBoundsChanged;
        }

        public void PostProcessScene(Transform sceneRoot)
        {
            StartCoroutine(PostProcessInternal(sceneRoot));
        }

        private IEnumerator PostProcessInternal(Transform sceneRoot)
        {
            Bounds GetMeshBounds(Transform meshFilterTransform, List<Vector3> vertices)
            {
                Bounds sceneMeshBounds = default;

                for (var i = 0; i < vertices.Count; i++)
                {
                    var worldPos = meshFilterTransform.TransformPoint(vertices[i]);

                    if (i == 0)
                    {
                        sceneMeshBounds = new Bounds(worldPos, Vector3.zero);
                        continue;
                    }

                    sceneMeshBounds.Encapsulate(worldPos);
                }

                return sceneMeshBounds;
            }

            do
            {
                yield return null;
            } while (!_boundsUpdated);

            var room = FindObjectOfType<OVRSceneRoom>();

            Assert.IsNotNull(room);

            while (room.Walls.Length == 0) yield return null;

            Debug.Log("Post-processing scene.");
            var semanticClassifications = room.GetComponentsInChildren<OVRSemanticClassification>();

            // Keep track of meshes and bounding boxes separately
            List<OVRSceneVolume> sceneBoundingBoxes = new();
            List<OVRScenePlane> scenePlanes = new();

            List<OVRSemanticClassification> sceneMeshes = new();
            List<OVRSemanticClassification> walkableFurniture = new();
            List<OVRSemanticClassification> targetableFurniture = new();

            Bounds sceneMeshBounds = default;

            if (!room.TryGetComponent<SceneQuery>(out var sceneQuery))
            {
                sceneQuery = room.gameObject.AddComponent<SceneQuery>();
            }

            sceneQuery.Initialize(semanticClassifications);

            // All the scene objects we care about should have a semantic classification, regardless of type
            foreach (var semanticObject in semanticClassifications)
            {
                if (semanticObject.Contains(Classification.GlobalMesh))
                {
                    // To support using static mesh on device.
                    if (semanticObject.TryGetComponent<OVRSceneVolumeMeshFilter>(out var volumeMeshFilter)
                        && volumeMeshFilter.enabled)
                        yield return new WaitUntil(() => volumeMeshFilter.IsCompleted);

                    var meshFilter = semanticObject.GetComponent<MeshFilter>();
                    var vertices = new List<Vector3>();

                    do
                    {
                        yield return null;
                        meshFilter.sharedMesh.GetVertices(vertices);
                    } while (vertices.Count == 0);

                    sceneMeshBounds = GetMeshBounds(meshFilter.transform, vertices);
#if UNITY_EDITOR
                    if (meshFilter == null) meshFilter = semanticObject.GetComponentInChildren<MeshFilter>();

                    if (meshFilter == null)
                        Debug.LogError("No mesh filter on object classified as SceneMesh.", semanticObject);

                    if (semanticObject.TryGetComponent<MeshCollider>(out var meshCollider))
                        while (meshCollider.sharedMesh == null)
                        {
                            Debug.Log("waiting for mesh collider bake!");
                            yield return null;
                        }

                    yield return null;
#endif

                    Debug.Log($"Scene mesh found with {meshFilter.sharedMesh.triangles.Length} triangles.");
                    sceneMeshes.Add(semanticObject);
                    continue;
                }

                if (semanticObject.ContainsAny(WalkableFurniture))
                {
                    // Need to make sure floor is set up before furniture is set up.
                    walkableFurniture.Add(semanticObject);
                }
                else if (semanticObject.ContainsAny(RangedTargets) || semanticObject.ContainsAny(WallMountedTargets))
                {
                    targetableFurniture.Add(semanticObject);
                }

                if (semanticObject.TryGetComponent<OVRSceneVolume>(out var sceneVolume))
                    sceneBoundingBoxes.Add(sceneVolume);
                else if (semanticObject.TryGetComponent<OVRScenePlane>(out var scenePlane)) scenePlanes.Add(scenePlane);
            }

            if (sceneMeshes.Count == 0)
            {
                // have to wait until the floor's boundary is loaded for meshing to work.
                while (room.Floor.Boundary.Count == 0)
                {
                    yield return null;
                }

                var semanticClassification = CreateFallbackSceneMesh(room);
                if (semanticClassification != null)
                {
                    sceneMeshes.Add(semanticClassification);

                    if (semanticClassification.TryGetComponent<MeshFilter>(out var meshFilter))
                    {
                        var vertices = new List<Vector3>();
                        meshFilter.sharedMesh.GetVertices(vertices);

                        sceneMeshBounds = GetMeshBounds(meshFilter.transform, vertices);
                    }
                }

                // give the new mesh colliders time to bake
                yield return new WaitForFixedUpdate();
            }

            if (hideSceneMesh && sceneMeshes.Count > 0)
            {
                Debug.Log("Disabling scene mesh.");
                foreach (var sceneMesh in sceneMeshes) sceneMesh.gameObject.SetActive(false);
            }

            if (generateNavMesh)
            {
                Transform meshTransform;
                var furnitureInScene = false;
                if (sceneMeshes.Count == 0)
                {
                    Debug.LogWarning("No scene mesh found in scene.");
                    meshTransform = room.Floor.transform;
                }
                else
                {
                    furnitureInScene = walkableFurniture.Count > 0;
                    meshTransform = sceneMeshes[0].transform;
                }

                PrepareNavmesh(meshTransform, room, sceneMeshBounds, furnitureInScene);

                foreach (var furniture in walkableFurniture)
                {
                    Debug.Log($"Preparing furniture for: {string.Join(",", furniture.Labels)}");
                    if (!PrepareFurniture(furniture))
                    {
                        // no navmesh was generated for this piece of furniture.
                        // mark it as targetable instead of walkable.
                        Debug.Log("Marked as targetable");
                        targetableFurniture.Add(furniture);
                    }
                    else
                    {
                        Debug.Log("Marked as walkable");
                    }
                }

                foreach (var furniture in targetableFurniture) PrepareTargetableFurniture(furniture, room);

                // At this point all furniture is set up and we can validate reachability.
                yield return StartCoroutine(NavMeshBookKeeper.ValidateScene(room));
            }

            SceneDataProcessed?.Invoke(sceneRoot);
            yield return null;
        }

        private OVRSemanticClassification CreateFallbackSceneMesh(OVRSceneRoom ovrSceneRoom)
        {
            var sceneDataLoader = GetComponent<SceneDataLoader>();

            // get the SceneMesh prefab from the OVRSceneManager;
            var sceneMeshPrefab = sceneDataLoader.GetSceneMeshPrefab();

            if (sceneMeshPrefab == null)
            {
                Debug.LogError("No global mesh prefab override found");
                return null;
            }

            var fallbackInstance = Instantiate(sceneMeshPrefab);
            var go = SceneMesher.CreateMesh(ovrSceneRoom, fallbackInstance.gameObject, 0.01f, true);

            if (go.TryGetComponent<OVRSceneAnchor>(out var anchor))
            {
                SceneDataLoader.AddAnchorReferenceCount(anchor);
            }

            go.transform.SetParent(ovrSceneRoom.transform);

            return go.GetComponent<OVRSemanticClassification>();
        }

        private void PrepareTargetableFurniture(OVRSemanticClassification classification, OVRSceneRoom room)
        {
            PhantomTarget targetable;

            if (!classification.TryGetComponent(out targetable))
            {
                targetable = Instantiate(rangedFurnitureTargetPrefab, classification.transform);
            }

            targetable.Initialize(classification, room);
        }

        private bool PrepareFurniture(OVRSemanticClassification classification)
        {
            var furnitureNavmesh = Instantiate(furnitureNavMeshGeneratorPrefab, classification.transform);
            if (!furnitureNavmesh.Initialize(classification))
            {
                furnitureNavmesh.Destruct();
                return false;
            }

            return true;
        }

        private void PrepareNavmesh(Transform meshTransform, OVRSceneRoom room, Bounds meshBounds,
            bool furnitureInScene)
        {
            var navmeshGenerator = Instantiate(navMeshGeneratorPrefab, meshTransform);
            navmeshGenerator.Initialize(room, meshBounds, furnitureInScene);
        }

        private void OnBoundsChanged(Bounds bounds)
        {
            _boundsUpdated = true;
        }
    }
}
