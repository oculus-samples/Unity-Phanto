// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using Meta.XR.EnvironmentDepth;
using PhantoUtils;
using UnityEngine;

public class OcclusionDemoController : MonoBehaviour
{
    private const string HARD_OCCLUSION = "HARD_OCCLUSION";
    private const string SOFT_OCCLUSION = "SOFT_OCCLUSION";

    [SerializeField]
    private Transform trio;

    private EnvironmentDepthManager _environmentDepthManager;
    private bool _ready;

    private void Awake()
    {
        if (!TryGetComponent(out _environmentDepthManager))
        {
            Debug.LogError($"Requires a {nameof(EnvironmentDepthManager)} component.");
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
        if (EnvironmentDepthManager.IsSupported)
        {
            _environmentDepthManager.enabled = true;
        }

        // Enable the ghosts so their per object shader keywords are enabled after global disable.
        trio.gameObject.SetActive(true);
    }
}
