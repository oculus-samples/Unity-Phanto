// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using Phantom;
using UnityEngine;

[MetaCodeSample("Phanto")]
public class ShowWireframe : MonoBehaviour
{
    private void Awake()
    {
        SceneVisualizationManager.ShowWireframe += Show;
    }

    private void OnDestroy()
    {
        SceneVisualizationManager.ShowWireframe -= Show;
    }

    private void Show(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
