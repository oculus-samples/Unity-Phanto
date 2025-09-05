// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Meta.XR.MRUtilityKit;
using OVRSimpleJSON;
using PhantoUtils;
using Unity.VisualScripting;
using UnityEngine;
using Object = UnityEngine.Object;

[MetaCodeSample("Phanto")]
public static class JsonSceneBuilder
{
    private static ulong _handleCounter = long.MaxValue;

    public static ulong NextHandle => --_handleCounter;

    /// <summary>
    ///     Set uuid of anchor to handle for its scene.
    /// </summary>
    public static OVRSpace SetUuid(MRUKAnchor anchor, Guid uuid, ulong handle)
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

}
