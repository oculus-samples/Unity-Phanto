// Copyright (c) Meta Platforms, Inc. and affiliates.

using Phantom;
using UnityEngine;

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
