// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OVRSimpleJSON;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;

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

        private static readonly string GuidEmpty = Guid.Empty.ToString();

        // Scene API data
        private static readonly string[] MeshClassifications =
            { "GlobalMesh", OVRSceneManager.Classification.GlobalMesh };

        private static readonly Dictionary<OVRSpace, int> SpaceDictionary = new Dictionary<OVRSpace, int>();

        // A reference to the OVRSceneManager prefab.
        [SerializeField] private OVRSceneManager ovrSceneManagerPrefab;

        // The root transform of the OVRSceneManager.
        [SerializeField] private Transform sceneRoot;

        [SerializeField] private SceneDataLoaderSettings settings;

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
        public void LoadStaticMesh(string jsonText)
        {
            var json = JSON.Parse(jsonText);

            var instantiatedMesh = SpawnSceneRoom(json, sceneRoot);

            if (settings.CenterStaticMesh)
                // Center mesh on tracking space.
                AlignStaticMesh(instantiatedMesh);
            SceneDataLoaded?.Invoke(sceneRoot);

            var debugSceneEntities = gameObject.AddComponent<DebugSceneEntities>();
            debugSceneEntities.StaticSceneModelLoaded();
        }

        /// <summary>
        /// Create a game object for a room in the scene.
        /// </summary>
        private Transform SpawnSceneRoom(JSONNode json, Transform root)
        {
            SpaceDictionary.Clear();

            var roomNode = json.GetValueOrDefault("room", null);
            if (roomNode != null)
            {
                GenerateRoom(root, roomNode);
                return root;
            }

            var rooms = json.GetValueOrDefault("rooms", null);
            if (rooms != null)
            {
                for (int i = 0; i < rooms.Count; i++)
                {
                    GenerateRoom(root, rooms[i]);
                }

                return root;
            }

            // JSON format that can be used with Meta XR Simulator scene recorder
            // https://developer.oculus.com/documentation/unity/xrsim-scene-recorder/
            // NOTE: This format doesn't contain scene mesh data.
            var components = json.GetValueOrDefault("components", null) as JSONObject;
            if (components != null)
            {
                rooms = XRSimConverter.Convert(components);

                for (int i = 0; i < rooms.Count; i++)
                {
                    GenerateRoom(root, rooms[i]);
                }

                return root;
            }

            throw new ArgumentException("Unknown json format");
        }

        private void GenerateRoom(Transform root, JSONNode roomNode)
        {
            var pose = ToPose(roomNode["pose"]);
            var uuid = roomNode["uuid"].Value.Substring(0, 6);

            var roomGo = new GameObject($"JsonRoom_{uuid}");
            var roomTransform = roomGo.transform;
            roomTransform.SetPositionAndRotation(pose.position, pose.rotation);
            roomTransform.SetParent(root);

            var room = roomGo.AddComponent<OVRSceneRoom>();
            room.enabled = false;

            var sceneChildren = (JSONArray)roomNode["children"];

            var planeList = new List<OVRScenePlane>();

            // Add each child node to the room.
            for (var i = 0; i < sceneChildren.Count; i++)
            {
                var child = sceneChildren[i];
                if (child["uuid"].Value == GuidEmpty)
                {
                    continue;
                }

                var space = SpawnSceneChild((JSONObject)child, room, roomTransform, planeList);

                SpaceDictionary[space] = 1;
            }

            AddPlanesToRoom(room, planeList);

            var anchorReferenceCountDictionary = typeof(OVRSceneAnchor).GetField("AnchorReferenceCountDictionary",
                BindingFlags.NonPublic | BindingFlags.Static);

            anchorReferenceCountDictionary?.SetValue(null, SpaceDictionary);
        }


        /// <summary>
        /// Create a game object for a child node.
        /// </summary>
        private OVRSpace SpawnSceneChild(JSONObject child, OVRSceneRoom room, Transform parent,
            List<OVRScenePlane> planeList)
        {
            // Get all the child nodes in the hierarchy.
            var uuidNode = child["uuid"];
            var handleNode = child["handle"];
            var classificationNode = child["classification"];
            var volumeNode = child["volume"];
            var planeNode = child["plane"];
            var meshNode = child["mesh"];
            var pose = ToPose(child["pose"]);

            // Create the child object.
            var overrides = ovrSceneManagerPrefab.PrefabOverrides;
            var volumePrefab = ovrSceneManagerPrefab.VolumePrefab;
            var planePrefab = ovrSceneManagerPrefab.PlanePrefab;

            OVRSceneAnchor anchorPrefab = null;

            if (classificationNode != null)
            {
                var classifcation = classificationNode[0].Value;

                foreach (var prefabOverride in overrides)
                    if (prefabOverride.ClassificationLabel == classifcation)
                    {
                        anchorPrefab = prefabOverride.Prefab;
                        break;
                    }
            }

            if (anchorPrefab == null)
            {
                if (volumeNode != null)
                    anchorPrefab = volumePrefab;
                else
                    anchorPrefab = planePrefab;
            }

            var anchorInstance = Instantiate(anchorPrefab, pose.position, pose.rotation, parent);
            // Set classification and label.
            SetClassification(anchorInstance, (JSONArray)classificationNode);

            OVRScenePlane plane = null;
            OVRSceneVolume volume = null;

            if (planeNode != null)
            {
                plane = SetPlane(anchorInstance, (JSONObject)planeNode);

                planeList.Add(plane);
            }

            if (volumeNode != null)
            {
                volume = SetVolume(anchorInstance, (JSONObject)volumeNode);
            }

            if (meshNode != null) SetMesh(anchorInstance, (JSONObject)meshNode);

            var space = SetUuid(anchorInstance, uuidNode.Value, handleNode.Value);

            if (volume != null)
            {
                // This triggers the resizing of the instantiated volume
                volume.ScaleChildren = volume.ScaleChildren;
                volume.OffsetChildren = volume.OffsetChildren;
            }

            if (plane != null)
            {
                // This triggers the resizing of the instantiated plane
                plane.ScaleChildren = plane.ScaleChildren;
                plane.OffsetChildren = plane.OffsetChildren;
            }

            return space;
        }

        /// <summary>
        /// Add a plane to an existing room.
        /// </summary>
        private static void AddPlanesToRoom(OVRSceneRoom room, List<OVRScenePlane> planeList)
        {
            var walls = new List<OVRScenePlane>();

            var roomType = room.GetType();

            var updateRoomMethod = roomType.GetMethod("UpdateRoomInformation",
                BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var plane in planeList)
            {
                if (plane.TryGetComponent<OVRSemanticClassification>(out var classification) &&
                    classification.Labels[0] == OVRSceneManager.Classification.WallFace)
                    walls.Add(plane);

                updateRoomMethod?.Invoke(room, new object[] { plane });
            }

            var wallsProperty = roomType.GetProperty("Walls",
                BindingFlags.Public | BindingFlags.Instance);
            wallsProperty?.SetValue(room, walls.ToArray());
        }

        private void SetMesh(OVRSceneAnchor anchor, JSONObject meshNode)
        {
            var vertsNode = (JSONArray)meshNode["verts"];
            var normalsNode = (JSONArray)meshNode["normals"];
            var trianglesNode = (JSONArray)meshNode["triangles"];
            var uvsNode = (JSONArray)meshNode["uvs"];

            var verts = new Vector3[vertsNode.Count];
            for (var i = 0; i < verts.Length; i++)
            {
                verts[i] = vertsNode[i];
            }

            var normals = new Vector3[normalsNode.Count];
            for (var i = 0; i < normals.Length; i++)
            {
                normals[i] = normalsNode[i];
            }

            var triangles = new int[trianglesNode.Count];
            for (var i = 0; i < triangles.Length; i++)
            {
                triangles[i] = trianglesNode[i];
            }

            var uvs = new Vector2[uvsNode.Count];
            for (var i = 0; i < uvs.Length; i++)
            {
                uvs[i] = uvsNode[i];
            }

            if (anchor.TryGetComponent<OVRSceneVolumeMeshFilter>(out var volumeMeshFilter))
            {
                volumeMeshFilter.enabled = false;

                var volumeMeshFilterType = volumeMeshFilter.GetType();
                var isCompletedProp = volumeMeshFilterType.GetProperty("IsCompleted",
                    BindingFlags.Public | BindingFlags.Instance);
                isCompletedProp?.SetValue(volumeMeshFilter, true);
            }

            if (anchor.TryGetComponent<OVRScenePlaneMeshFilter>(out var planeMeshFilter))
                planeMeshFilter.enabled = false;

            var mesh = new Mesh
            {
                name = $"MockMesh_{(ushort)anchor.Space.Handle:X4}"
            };

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.uv = uvs;

            mesh.RecalculateBounds();

            if (anchor.TryGetComponent<MeshFilter>(out var meshFilter)) meshFilter.sharedMesh = mesh;

            if (anchor.TryGetComponent<MeshCollider>(out var meshCollider))
            {
                Physics.BakeMesh(mesh.GetInstanceID(), false);

                meshCollider.sharedMesh = mesh;
            }
        }

        /// <summary>
        /// Create a scene plane and add it to the room.
        /// </summary>
        private OVRScenePlane SetPlane(OVRSceneAnchor anchor, JSONObject planeNode)
        {
            if (!anchor.TryGetComponent<OVRScenePlane>(out var plane)) plane = anchor.AddComponent<OVRScenePlane>();

            plane.enabled = false;

            Vector2 dimensions = planeNode["dimensions"];
            Vector2 offset = planeNode["offset"];

            // Setting these values via reflection because there are no public setters.
            var planeType = typeof(OVRScenePlane);

            var anchorField = planeType.GetField("_sceneAnchor",
                BindingFlags.NonPublic | BindingFlags.Instance);
            anchorField?.SetValue(plane, anchor);

            var widthProp = planeType.GetProperty("Width",
                BindingFlags.Public | BindingFlags.Instance);
            widthProp?.SetValue(plane, dimensions.x);

            var heightProp = planeType.GetProperty("Height",
                BindingFlags.Public | BindingFlags.Instance);
            heightProp?.SetValue(plane, dimensions.y);

            var offsetProp = planeType.GetProperty("Offset",
                BindingFlags.Public | BindingFlags.Instance);
            offsetProp?.SetValue(plane, offset);

            var boundaryNode = (JSONArray)planeNode["boundary"];
            var boundaryList = new List<Vector2>(boundaryNode.Count);

            for (var i = 0; i < boundaryNode.Count; i++)
            {
                boundaryList.Add(boundaryNode[i]);
            }

            var boundaryProp = planeType.GetField("_boundary",
                BindingFlags.NonPublic | BindingFlags.Instance);
            boundaryProp?.SetValue(plane, boundaryList);

            plane.ScaleChildren = planeNode["scaleChildren"];
            plane.OffsetChildren = planeNode["offsetChildren"];

            return plane;
        }

        /// <summary>
        /// Set the volume of a room and its children.
        /// </summary>
        private OVRSceneVolume SetVolume(OVRSceneAnchor anchor, JSONObject volumeNode)
        {
            if (!anchor.TryGetComponent<OVRSceneVolume>(out var volume)) volume = anchor.AddComponent<OVRSceneVolume>();

            volume.enabled = false;

            Vector3 dimensions = volumeNode["dimensions"];
            Vector3 offset = volumeNode["offset"];

            // Setting these values via reflection because there are no public setters.
            var volumeType = typeof(OVRSceneVolume);
            var anchorField = volumeType.GetField("_sceneAnchor",
                BindingFlags.NonPublic | BindingFlags.Instance);
            anchorField?.SetValue(volume, anchor);

            var widthProp = volumeType.GetProperty("Width",
                BindingFlags.Public | BindingFlags.Instance);
            widthProp?.SetValue(volume, dimensions.x);

            var heightProp = volumeType.GetProperty("Height",
                BindingFlags.Public | BindingFlags.Instance);
            heightProp?.SetValue(volume, dimensions.y);

            var depthProp = volumeType.GetProperty("Depth",
                BindingFlags.Public | BindingFlags.Instance);
            depthProp?.SetValue(volume, dimensions.z);

            var offsetProp = volumeType.GetProperty("Offset",
                BindingFlags.Public | BindingFlags.Instance);
            offsetProp?.SetValue(volume, offset);

            volume.ScaleChildren = volumeNode.GetValueOrDefault("scaleChildren", volume.ScaleChildren);
            volume.OffsetChildren = volumeNode.GetValueOrDefault("offsetChildren", volume.OffsetChildren);

            return volume;
        }


        /// <summary>
        /// Set classification of the anchor to global or semantic labels.
        /// </summary>
        private static void SetClassification(OVRSceneAnchor anchor, JSONArray classificationNode)
        {
            if (!anchor.TryGetComponent<OVRSemanticClassification>(out var classification))
                classification = anchor.AddComponent<OVRSemanticClassification>();

            var list = new List<string>();

            for (var i = 0; i < classificationNode.Count; i++)
            {
                list.Add(classificationNode[i].Value);
            }

            var labelsField = classification.GetType().GetField("_labels",
                BindingFlags.NonPublic | BindingFlags.Instance);
            labelsField?.SetValue(classification, list);
        }

        /// <summary>
        ///     Set uuid of anchor to handle for its scene.
        /// </summary>
        private static OVRSpace SetUuid(OVRSceneAnchor anchor, string uuidString, string handleString)
        {
            // Setting these values via reflection because there are no public setters.
            var anchorType = anchor.GetType();
            var uuidProp = anchorType.GetProperty("Uuid",
                BindingFlags.Public | BindingFlags.Instance);
            uuidProp?.SetValue(anchor, new Guid(uuidString));

            var ovrSpace = new OVRSpace(Convert.ToUInt64(handleString, 16));

            var spaceProp = anchorType.GetProperty("Space",
                BindingFlags.Public | BindingFlags.Instance);
            spaceProp?.SetValue(anchor, ovrSpace);

            return ovrSpace;
        }

        /// <summary>
        ///     Convert JSON node to a pose object.
        /// </summary>
        private static Pose ToPose(JSONNode jsonNode)
        {
            Vector4 vector4 = jsonNode["rot"];

            var pose = new Pose
            {
                position = jsonNode["pos"],
                rotation = new Quaternion(vector4.x, vector4.y, vector4.z, vector4.w)
            };

            return pose;
        }

        /// <summary>
        ///     Shift static mesh so it's centered around the origin and floors match.
        /// </summary>
        /// <param name="root"></param>
        private void AlignStaticMesh(Transform root)
        {
            var meshFilters = root.GetComponentsInChildren<MeshFilter>();

            MeshFilter globalMesh = null;

            if (meshFilters.Length == 1)
                globalMesh = meshFilters[0];
            else
                foreach (var mf in meshFilters)
                {
                    if (mf.TryGetComponent<SimulatedObject>(out var simObject)
                        && simObject.CurrentClass == OVRSceneManager.Classification.GlobalMesh)
                    {
                        globalMesh = mf;
                        break;
                    }

                    if (mf.TryGetComponent<OVRSemanticClassification>(out var semanticClassification)
                        && semanticClassification.Labels[0] == OVRSceneManager.Classification.GlobalMesh)
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
