// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Depth;
using PhantoUtils;
using UnityEngine;

public class OcclusionSupportCheck : MonoBehaviour
{
    private void Start()
    {
        if (!OcclusionKeywordToggle.SupportsOcclusion && TryGetComponent<EnvironmentDepthOcclusionController>(out var depthOcclusionController))
        {
            depthOcclusionController.EnableOcclusionType(OcclusionType.NoOcclusion);
        }
    }
}
