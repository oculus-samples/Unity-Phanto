// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.EnvironmentDepth;
using PhantoUtils;
using UnityEngine;

public class OcclusionSupportCheck : MonoBehaviour
{
    private void Start()
    {
        if (!EnvironmentDepthManager.IsSupported && TryGetComponent<EnvironmentDepthManager>(out var depthOcclusionController))
        {
            depthOcclusionController.OcclusionShadersMode = OcclusionShadersMode.None;
        }
    }
}
