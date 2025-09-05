// Copyright (c) Meta Platforms, Inc. and affiliates.

using Common;
using Meta.XR.Samples;
using UnityEngine;

[MetaCodeSample("Phanto")]
public class DebugGameObjectToggle : MonoBehaviour
{
    private void Awake()
    {
        DebugLogPanelControls.DebugMenuEvent += DebugMenuToggle;
        DebugMenuToggle(false);
    }

    private void OnDestroy()
    {
        DebugLogPanelControls.DebugMenuEvent -= DebugMenuToggle;
    }

    private void DebugMenuToggle(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
