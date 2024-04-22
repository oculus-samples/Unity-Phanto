// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PhantoUtils
{
    public static class OVRExtensions
    {
        public static bool ContainsAny(this OVRSemanticClassification classification, IReadOnlyList<string> comparison)
        {
            foreach (var item in comparison)
            {
                if (classification.Contains(item))
                {
                    return true;
                }
            }

            return false;
        }

        public static Vector3 ClosetPointOnPlane(this OVRScenePlane scenePlane, Vector3 point)
        {
            var planeTransform = scenePlane.transform;
            var plane = new Plane(planeTransform.forward, planeTransform.position);
            return plane.ClosestPointOnPlane(point);
        }
    }
}
