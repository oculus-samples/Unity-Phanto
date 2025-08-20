// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Meta.XR.MRUtilityKit;
using PhantoUtils;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;

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
            MRUKAnchor.SceneLabels.TABLE.ToString(), MRUKAnchor.SceneLabels.COUCH.ToString(),
            MRUKAnchor.SceneLabels.OTHER.ToString(), MRUKAnchor.SceneLabels.STORAGE.ToString(),
            MRUKAnchor.SceneLabels.BED.ToString(),
        };

        // Items in the room that phantoms shouldn't hop on
        private static readonly string[] RangedTargets =
        {
            MRUKAnchor.SceneLabels.SCREEN.ToString(),
            MRUKAnchor.SceneLabels.LAMP.ToString(),
            MRUKAnchor.SceneLabels.PLANT.ToString(),
        };

        // Items in the room that phantoms can spit goo at
        private static readonly string[] WallMountedTargets =
        {
            MRUKAnchor.SceneLabels.WALL_ART.ToString(),
            MRUKAnchor.SceneLabels.WINDOW_FRAME.ToString(),
            MRUKAnchor.SceneLabels.DOOR_FRAME.ToString(),
        };

        [SerializeField] private NavMeshGenerator navMeshGeneratorPrefab;
        [SerializeField] private FurnitureNavMeshGenerator furnitureNavMeshGeneratorPrefab;
        [SerializeField] private RangedFurnitureTarget rangedFurnitureTargetPrefab;

        [SerializeField] private UnityEvent<Transform> SceneDataProcessed = new();

        [Tooltip("Remove scene mesh and replace with SceneMesher generated geometry.")]
        [SerializeField]
        private bool forceSceneMesher = false;

        [SerializeField] private bool hideSceneMesh;
        [SerializeField] private bool generateNavMesh = true;

        private readonly List<MRUKAnchor> _semanticClassifications =
            new List<MRUKAnchor>();

        private bool _sceneReady;

        private void OnEnable()
        {
            SceneBoundsChecker.WorldAligned += OnWorldAligned;
        }

        private void OnDisable()
        {
            SceneBoundsChecker.WorldAligned -= OnWorldAligned;
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

            // Wait for world alignment to finish.
            do
            {
                yield return null;
            } while (!_sceneReady);

            var rooms = GetComponentsInChildren<MRUKRoom>(true);

            Assert.IsTrue(rooms.Length > 0);

            // Process each room, generate navmesh, modify scene mesh etc.
            foreach (var room in rooms)
            {
                while (room.WallAnchors.Count == 0) yield return null;

                Debug.Log($"Post-processing scene: {room.name}");

                List<MRUKAnchor> sceneMeshes = new();
                List<MRUKAnchor> walkableFurniture = new();
                List<MRUKAnchor> targetableFurniture = new();

                Bounds sceneMeshBounds = default;

                room.GetComponentsInChildren(true, _semanticClassifications);

                var sceneMeshAnchor = room.GlobalMeshAnchor;
                if (sceneMeshAnchor != null)
                {
                    // To support using static mesh on device.
                    // if (semanticObject.TryGetComponent<MeshFilter>(out var volumeMeshFilter)
                    // && volumeMeshFilter.enabled)
                    // yield return new WaitUntil(() => volumeMeshFilter.IsCompleted);

                    var meshFilter = sceneMeshAnchor.GetComponentInChildren<MeshFilter>();
                    meshFilter.sharedMesh = sceneMeshAnchor.GlobalMesh;
                    var vertices = new List<Vector3>();
                    var meshIndices = room.GlobalMeshAnchor.GlobalMesh.triangles;
                    do
                    {
                        yield return null;
                        meshFilter.sharedMesh.GetVertices(vertices);
                    } while (vertices.Count == 0);

                    sceneMeshBounds = GetMeshBounds(room.GlobalMeshAnchor.transform, sceneMeshAnchor.GlobalMesh.vertices.ToList());
#if UNITY_EDITOR
                        if (meshFilter == null) meshFilter = sceneMeshAnchor.GetComponentInChildren<MeshFilter>();

                        if (meshFilter == null)
                            Debug.LogError("No mesh filter on object classified as SceneMesh.", sceneMeshAnchor);

                        if (sceneMeshAnchor.TryGetComponent<MeshCollider>(out var meshCollider))
                            while (meshCollider.sharedMesh == null)
                            {
                                Debug.Log("waiting for mesh collider bake!");
                                yield return null;
                            }

                        yield return null;
#endif

                    Debug.Log($"Scene mesh found with {sceneMeshAnchor.GlobalMesh.triangles.Length} triangles.");
                    sceneMeshes.Add(sceneMeshAnchor);
                }

                // Wait a frame to scene mesh to process
                yield return null;

                // All the scene objects we care about should have a semantic classification, regardless of type
                foreach (var semanticObject in _semanticClassifications)
                {
                    if (semanticObject.Label.ContainsAny(WalkableFurniture))
                    {
                        // Need to make sure floor is set up before furniture is set up.
                        walkableFurniture.Add(semanticObject);
                    }
                    else if (semanticObject.ContainsAny(RangedTargets) ||
                             semanticObject.ContainsAny(WallMountedTargets))
                    {
                        targetableFurniture.Add(semanticObject);
                    }
                }

                if (forceSceneMesher)
                {
                    // Destroy the instantiated scene meshes so we can replace them with SceneMesher objects.
                    for (var i = 0; i < sceneMeshes.Count; i++)
                    {
                        // FIXME: Disable instead of destroy?
                        Destroy(sceneMeshes[i].gameObject);
                    }

                    sceneMeshes.Clear();
                }

                if (sceneMeshes.Count == 0)
                {
                    // have to wait until the floor's boundary is loaded for meshing to work.
                    while (room.FloorAnchor.PlaneBoundary2D.Count == 0)
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
                        meshTransform = room.FloorAnchor.transform;
                    }
                    else
                    {
                        furnitureInScene = walkableFurniture.Count > 0;
                        meshTransform = sceneMeshes[0].transform;
                    }

                    PrepareNavmesh(meshTransform, room, sceneMeshBounds, furnitureInScene);

                    foreach (var furniture in walkableFurniture)
                    {
                        Debug.Log($"Preparing furniture for: {string.Join(",", furniture.Label)}");
                        if (!PrepareFurniture(furniture))
                        {
                            // no navmesh was generated for this piece of furniture.
                            // mark it as targetable instead of walkable.
                            // Debug.Log("Marked as targetable");
                            targetableFurniture.Add(furniture);
                        }
                        else
                        {
                            // Debug.Log("Marked as walkable");
                        }
                    }

                    foreach (var furniture in targetableFurniture)
                    {
                        PrepareTargetableFurniture(furniture, room);
                    }
                }
            }

            // At this point all furniture is set up and we can validate reachability.
            yield return StartCoroutine(NavMeshBookKeeper.ValidateRooms(rooms));

            SceneDataProcessed?.Invoke(sceneRoot);
        }

        private MRUKAnchor CreateFallbackSceneMesh(MRUKRoom MRUKRoom)
        {
            var sceneDataLoader = GetComponent<SceneDataLoader>();

            // get the SceneMesh prefab from the MRUK;
            var sceneMeshPrefab = sceneDataLoader.GetSceneMeshPrefab();

            if (sceneMeshPrefab == null)
            {
                Debug.LogError("No global mesh prefab override found");
                return null;
            }

            var fallbackInstance = Instantiate(sceneMeshPrefab);
            var go = SceneMesher.CreateMesh(MRUKRoom, fallbackInstance, 0.01f, true);

            if (go.TryGetComponent<MRUKAnchor>(out var anchor))
            {
                // set the anchor handle id.
                JsonSceneBuilder.SetUuid(anchor, Guid.NewGuid(), JsonSceneBuilder.NextHandle);

                SceneDataLoader.AddAnchorReferenceCount(anchor);
            }

            go.transform.SetParent(MRUKRoom.transform);

            return go.GetComponent<MRUKAnchor>();
        }

        private void PrepareTargetableFurniture(MRUKAnchor classification, MRUKRoom room)
        {
            PhantomTarget targetable;

            if (!classification.TryGetComponent(out targetable))
            {
                targetable = Instantiate(rangedFurnitureTargetPrefab, classification.transform);
            }

            targetable.Initialize(classification, room);
        }

        private bool PrepareFurniture(MRUKAnchor classification)
        {
            var furnitureNavmesh = Instantiate(furnitureNavMeshGeneratorPrefab, classification.transform);
            if (!furnitureNavmesh.Initialize(classification))
            {
                furnitureNavmesh.Destruct();
                return false;
            }

            return true;
        }

        private void PrepareNavmesh(Transform meshTransform, MRUKRoom room, Bounds meshBounds,
            bool furnitureInScene)
        {
            var navmeshGenerator = Instantiate(navMeshGeneratorPrefab, meshTransform);
            navmeshGenerator.Initialize(room, meshBounds, furnitureInScene);
        }

        private void OnWorldAligned()
        {
            _sceneReady = true;
        }


    }
}
