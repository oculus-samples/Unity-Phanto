// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Reflection;
using OVRSimpleJSON;
using PhantoUtils;
using Unity.VisualScripting;
using UnityEngine;
using Classification = OVRSceneManager.Classification;
using Object = UnityEngine.Object;

public static class JsonSceneBuilder
{
    private static ulong _handleCounter = long.MaxValue;

    private static readonly Dictionary<OVRSpace, int> SpaceDictionary = new Dictionary<OVRSpace, int>();

    private static readonly string GuidEmpty = Guid.Empty.ToString();

    private static readonly string[] WallTypes =
        { Classification.WallFace, Classification.InvisibleWallFace };

    public static ulong NextHandle => --_handleCounter;

    /// <summary>
    /// Create a game object for a room in the scene.
    /// </summary>
    public static Transform SpawnSceneRoom(JSONNode json, Transform root, OVRSceneManager sceneManager)
    {
        SpaceDictionary.Clear();

        var roomNode = json.GetValueOrDefault("room", null);
        if (roomNode != null)
        {
            GenerateRoom(root, roomNode, sceneManager);
            return root;
        }

        var rooms = json.GetValueOrDefault("rooms", null);
        if (rooms != null)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                GenerateRoom(root, rooms[i], sceneManager);
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
                GenerateRoom(root, rooms[i], sceneManager);
            }

            return root;
        }

        throw new ArgumentException("Unknown json format");
    }

    private static void GenerateRoom(Transform root, JSONNode roomNode, OVRSceneManager sceneManager)
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

            var space = SpawnSceneChild((JSONObject)child, sceneManager, roomTransform, planeList);

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
    private static OVRSpace SpawnSceneChild(JSONObject child, OVRSceneManager sceneManager, Transform parent,
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
        var overrides = sceneManager.PrefabOverrides;
        var volumePrefab = sceneManager.VolumePrefab;
        var planePrefab = sceneManager.PlanePrefab;

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

        var anchorInstance = Object.Instantiate(anchorPrefab, pose.position, pose.rotation, parent);
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
        var floor = new OVRScenePlane();
        var ceiling = new OVRScenePlane();

        var roomType = room.GetType();

        var updateRoomMethod = roomType.GetMethod("UpdateRoomInformation",
            BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var plane in planeList)
        {
            if (plane.TryGetComponent<OVRSemanticClassification>(out var classification))
            {
                if (classification.ContainsAny(WallTypes))
                    walls.Add(plane);

                if (classification.Contains(Classification.Floor))
                    floor = plane;

                if (classification.Contains(Classification.Ceiling))
                    ceiling = plane;
            }

            updateRoomMethod?.Invoke(room, new object[] { plane });
        }

        var wallsProperty = roomType.GetProperty("Walls",
            BindingFlags.Public | BindingFlags.Instance);
        wallsProperty?.SetValue(room, walls.ToArray());

        var floorProperty = roomType.GetProperty("Floor",
            BindingFlags.Public | BindingFlags.Instance);
        floorProperty?.SetValue(room, floor);

        var ceilingProperty = roomType.GetProperty("Ceiling",
            BindingFlags.Public | BindingFlags.Instance);
        ceilingProperty?.SetValue(room, ceiling);
    }

    private static void SetMesh(OVRSceneAnchor anchor, JSONObject meshNode)
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
    private static OVRScenePlane SetPlane(OVRSceneAnchor anchor, JSONObject planeNode)
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
    private static OVRSceneVolume SetVolume(OVRSceneAnchor anchor, JSONObject volumeNode)
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
        var uuid = new Guid(uuidString);
        var handle = Convert.ToUInt64(handleString, 16);

        return SetUuid(anchor, uuid, handle);
    }

    /// <summary>
    ///     Set uuid of anchor to handle for its scene.
    /// </summary>
    public static OVRSpace SetUuid(OVRSceneAnchor anchor, Guid uuid, ulong handle)
    {
        // Setting these values via reflection because there are no public setters.
        var anchorType = anchor.GetType();
        var uuidProp = anchorType.GetProperty("Uuid",
            BindingFlags.Public | BindingFlags.Instance);
        uuidProp?.SetValue(anchor, uuid);

        var ovrSpace = new OVRSpace(handle);

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
}
