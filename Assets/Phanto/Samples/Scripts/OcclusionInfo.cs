// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.EnvironmentDepth;
using Meta.XR.Samples;
using PhantoUtils;
using TMPro;
using UnityEngine;

[MetaCodeSample("Phanto")]
public class OcclusionInfo : MonoBehaviour
{
    private const string Unsupported = "Occlusion is only supported on the Meta Quest 3.";

    [SerializeField] private TextMeshProUGUI text;

    private void Start()
    {
        if (!EnvironmentDepthManager.IsSupported)
        {
            text.text = Unsupported;
        }
    }
}
