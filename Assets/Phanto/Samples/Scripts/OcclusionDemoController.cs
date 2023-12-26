// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using Meta.XR.Depth;
using PhantoUtils;
using UnityEngine;

public class OcclusionDemoController : MonoBehaviour
{
    private const string HARD_OCCLUSION = "HARD_OCCLUSION";
    private const string SOFT_OCCLUSION = "SOFT_OCCLUSION";

    [SerializeField]
    private Transform trio;

    private EnvironmentDepthTextureProvider _depthTextureProvider;
    private EnvironmentDepthOcclusionController _occlusionController;
    private bool _ready;

    private void Awake()
    {
        if (!TryGetComponent(out _depthTextureProvider))
        {
            Debug.LogError($"Requires a {nameof(EnvironmentDepthTextureProvider)} component.");
            return;
        }

        if (!TryGetComponent(out _occlusionController))
        {
            Debug.LogError($"Requires a {nameof(EnvironmentDepthOcclusionController)} component.");
            return;
        }
    }

    private void OnEnable()
    {
        if (_ready)
        {
            DisableOcclusion();
        }
    }

    private void Start()
    {
        DisableOcclusion();
        _ready = true;
    }

    private void DisableOcclusion()
    {
        // Make sure occlusion is globally disabled so it can be enabled per object.
        _occlusionController.EnableOcclusionType(OcclusionType.NoOcclusion);

        // disable the global occlusion keywords so we can have per-material control.
        Shader.DisableKeyword(EnvironmentDepthOcclusionController.HardOcclusionKeyword);
        Shader.DisableKeyword(EnvironmentDepthOcclusionController.SoftOcclusionKeyword);

        if (OcclusionKeywordToggle.SupportsOcclusion)
        {
            _depthTextureProvider.SetEnvironmentDepthEnabled(true);
        }

        // Enable the ghosts so their per object shader keywords are enabled after global disable.
        trio.gameObject.SetActive(true);
    }
}
