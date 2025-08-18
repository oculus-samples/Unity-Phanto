// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using Meta.XR.EnvironmentDepth;
using PhantoUtils;
using TMPro;
using UnityEngine;

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
