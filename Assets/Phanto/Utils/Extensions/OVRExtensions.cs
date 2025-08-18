// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace PhantoUtils
{
    public static class OVRExtensions
    {
        public static bool ContainsAny(this MRUKAnchor.SceneLabels classification, IReadOnlyList<string> comparison)
        {
            foreach (var item in comparison)
            {
                if (classification.ToString().ToLower().Contains(item.ToLower()))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ContainsAny(this MRUKAnchor classification, IReadOnlyList<string> comparison)
        {
            foreach (var item in comparison)
            {
                if (classification.Label.ToString().ToLower().Contains(item.ToLower()))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool Contains(this MRUKAnchor classification, string comparison)
        {
            return classification.Label.ToString().ToLower().Contains(comparison.ToLower());
        }

        public static Vector3 ClosestPointOnPlane(this OVRScenePlane scenePlane, Vector3 point)
        {
            var planeTransform = scenePlane.transform;
            var plane = new Plane(planeTransform.forward, planeTransform.position);
            return plane.ClosestPointOnPlane(point);
        }
    }
}
