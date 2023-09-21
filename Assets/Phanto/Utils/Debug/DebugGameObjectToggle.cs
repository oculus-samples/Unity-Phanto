// Copyright (c) Meta Platforms, Inc. and affiliates.

using Common;
using UnityEngine;

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
