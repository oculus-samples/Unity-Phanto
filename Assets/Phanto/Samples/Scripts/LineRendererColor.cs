// Copyright (c) Meta Platforms, Inc. and affiliates.

using PhantoUtils;
using UnityEditor;
using UnityEngine;

public class LineRendererColor : MonoBehaviour
{
    [SerializeField] private Color color = MSPalette.Aqua;

    [SerializeField] private LineRenderer[] lineRenderers;

    private void Awake()
    {
        if (lineRenderers == null || lineRenderers.Length == 0)
        {
            FindLineRenderers();
        }

        UpdateColor(color);
    }

    private void FindLineRenderers()
    {
        lineRenderers = GetComponentsInChildren<LineRenderer>(true);
    }

    private void UpdateColor(Color lineColor)
    {
        foreach (var lr in lineRenderers)
        {
            lr.startColor = color;
            lr.endColor = color;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        FindLineRenderers();
    }

    private void OnValidate()
    {
        if (lineRenderers == null || lineRenderers.Length == 0)
        {
            FindLineRenderers();
        }

        if (EditorApplication.isPlaying)
        {
            UpdateColor(color);
        }
    }
#endif
}
