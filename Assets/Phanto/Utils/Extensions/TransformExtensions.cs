// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;

namespace PhantoUtils
{
    [MetaCodeSample("Phanto")]
    public static class TransformExtensions
    {
        public static void SetWorldScale(this Transform transform, Vector3 worldScale)
        {
            if (transform.parent == null)
            {
                transform.localScale = worldScale;
                return;
            }

            transform.localScale = worldScale.DivideBy(transform.parent.lossyScale);
        }
    }
}
