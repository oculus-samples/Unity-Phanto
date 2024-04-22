// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using OVRSimpleJSON;
using UnityEngine;
using UnityEngine.Assertions;

public static class XRSimConverter
{
    private const string Bounded2D = "Bounded2D";
    private const string Bounded3D = "Bounded3D";
    private const string Locatable = "Locatable";
    private const string RoomLayout = "RoomLayout";
    private const string SemanticLabels = "SemanticLabels";
    private const string SpaceContainer = "SpaceContainer";

    private const string ANCHOR = "anchor";
    private const string SPACES = "spaces";
    private const string UUID = "uuid";
    private const string HANDLE = "handle";
    private const string POSE = "pose";
    private const string CLASSIFICATION = "classification";
    private const string CHILDREN = "children";
    private const string PLANE = "plane";
    private const string VOLUME = "volume";
    private const string LABELS = "labels";

    private const string POSITION = "position";
    private const string ORIENTATION = "orientation";
    private const string RECT_2D = "rect2D";
    private const string RECT_3D = "rect3D";
    private const string EXTENT = "extent";
    private const string OFFSET = "offset";
    private const string DIMENSIONS = "dimensions";
    private const string BOUNDARY = "boundary";

    private const string SCALE_CHILDREN = "scaleChildren";
    private const string OFFSET_CHILDREN = "offsetChildren";

    private const string WIDTH = "width";
    private const string HEIGHT = "height";
    private const string DEPTH = "depth";

    private const string SCENE_ROOM = "SCENE_ROOM";

    private const string POS = "pos";
    private const string ROT = "rot";

    private static readonly Quaternion RotateY180 = Quaternion.Euler(0, 180, 0);

    private static long _handleCounter = 2024;

    private static string NextHandle => (++_handleCounter).ToString();

    public static JSONArray Convert(JSONObject components)
    {
        var rooms = new JSONArray();

        var spaceContainer = ParseComponent(components, SpaceContainer);
        var roomLayout = ParseComponent(components, RoomLayout);

        var bounded2d = ParseComponent(components, Bounded2D);
        var bounded3d = ParseComponent(components, Bounded3D);
        var locatable = ParseComponent(components, Locatable);
        var semanticLabels = ParseComponent(components, SemanticLabels);

        foreach (var (uuid, node) in spaceContainer)
        {
            // there should be a room layout with this uuid
            if (!roomLayout.TryGetValue(uuid, out var roomNode))
            {
                continue;
            }

            var room = new JSONObject
            {
                [UUID] = ConvertUuid(roomNode[ANCHOR]),
                [HANDLE] = NextHandle,
                [POSE] = ToPose(Vector3.zero, Quaternion.identity),
                [CLASSIFICATION] = SCENE_ROOM
            };

            var children = new JSONArray();

            var spacesArray = node[SPACES] as JSONArray;
            Assert.IsNotNull(spacesArray);

            for (int i = 0; i < spacesArray.Count; i++)
            {
                var spaceUuid = spacesArray[i].Value;

                if (!semanticLabels.TryGetValue(spaceUuid, out var labelNode) || !locatable.TryGetValue(spaceUuid, out var poseNode))
                {
                    // if it doesn't have a classification or pose we don't care about it.
                    continue;
                }

                var child = new JSONObject
                {
                    [UUID] = ConvertUuid(spaceUuid),
                    [HANDLE] = NextHandle,
                    [CLASSIFICATION] = SetClassification(labelNode),
                    [POSE] = SetPose(poseNode)
                };

                if (bounded3d.TryGetValue(spaceUuid, out var volumeNode))
                {
                    child[VOLUME] = SetVolume(volumeNode);
                }

                if (bounded2d.TryGetValue(spaceUuid, out var planeNode))
                {
                    child[PLANE] = SetPlane(planeNode);
                }

                children.Add(child);
            }

            room[CHILDREN] = children;
            rooms.Add(room);
        }

        return rooms;
    }

    private static JSONObject SetVolume(JSONObject volumeNode)
    {
        var rect3d = volumeNode.GetValueOrDefault(RECT_3D, null) as JSONObject;

        if (rect3d == null)
        {
            return new JSONObject();
        }

        var dimensions = rect3d[EXTENT].ReadVector3(WIDTH, HEIGHT, DEPTH);
        var offset = rect3d[OFFSET].ReadVector3();

        offset.x += dimensions.x * 0.5f;
        offset.y += dimensions.y * 0.5f;
        offset.z += dimensions.z;

        var volume = new JSONObject
        {
            [DIMENSIONS] = dimensions,
            [OFFSET] = offset,

            [SCALE_CHILDREN] = true,
            [OFFSET_CHILDREN] = true,
        };

        return volume;
    }

    private static JSONObject SetPlane(JSONObject planeNode)
    {
        var rect2d = planeNode.GetValueOrDefault(RECT_2D, null) as JSONObject;

        if (rect2d == null)
        {
            return new JSONObject();
        }

        var dimensions = rect2d[EXTENT].ReadVector2(WIDTH, HEIGHT);
        var offset = rect2d[OFFSET].ReadVector2();

        var halfDim = dimensions * 0.5f;
        offset += halfDim;

        var boundary = new JSONArray();
        boundary.Add(new Vector2(halfDim.x, -halfDim.y));
        boundary.Add(new Vector2(-halfDim.x, -halfDim.y));
        boundary.Add(new Vector2(-halfDim.x, halfDim.y));
        boundary.Add(new Vector2(halfDim.x, halfDim.y));

        var plane = new JSONObject
        {
            [DIMENSIONS] = dimensions,
            [OFFSET] = offset,
            [BOUNDARY] = boundary,

            [SCALE_CHILDREN] = true,
            [OFFSET_CHILDREN] = true,
        };

        return plane;
    }

    private static JSONObject SetPose(JSONObject poseNode)
    {
        var pose = poseNode.GetValueOrDefault(POSE, null) as JSONObject;

        if (pose == null)
        {
            return ToPose(Vector3.zero, Quaternion.identity);
        }

        var position = pose[POSITION].ReadVector3();
        // equivalent to Vector3f.FromFlippedZVector3f
        position.z = -position.z;

        var orientation = pose[ORIENTATION].ReadQuaternion();
        // equivalent to Quatf.FromFlippedZQuatf
        orientation.x = -orientation.x;
        orientation.y = -orientation.y;

        return ToPose(position, orientation * RotateY180);
    }

    private static JSONArray SetClassification(JSONObject labelNode)
    {
        return labelNode.GetValueOrDefault(LABELS, new JSONArray()) as JSONArray;
    }

    private static string ConvertUuid(JSONNode node)
    {
        return ConvertUuid(node.Value);
    }

    private static string ConvertUuid(string uuid)
    {
        var guid = new Guid(uuid);
        return guid.ToString();
    }

    private static JSONObject ToPose(Vector3 pos, Quaternion rot)
    {
        var pose = new JSONObject
        {
            [POS] = pos,
            [ROT] = rot,
            Inline = true
        };

        return pose;
    }

    private static Dictionary<string, JSONObject> ParseComponent(JSONObject components, string key)
    {
        var component = components.GetValueOrDefault(key, null) as JSONArray;
        Assert.IsNotNull(component);

        var count = component.Count;
        var result = new Dictionary<string, JSONObject>(count);

        for (int i = 0; i < count; i++)
        {
            var node = component[i] as JSONObject;
            Assert.IsNotNull(node);

            var uuid = node[ANCHOR].Value;

            if (!result.TryAdd(uuid, node))
            {
                Debug.LogWarning($"Duplicate element: {uuid} in {key} component");
            }
        }

        return result;
    }
}
