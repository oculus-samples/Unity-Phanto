// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using Common;
using UnityEngine;
using UnityEngine.Assertions;
using Utilities.XR;

[RequireComponent(typeof(OVRSceneManager))]
public class DebugSceneEntities : MonoBehaviour
{
    [SerializeField] private OVRSceneManager sceneManager;

    [SerializeField] private Color planeColor = Color.yellow;
    [SerializeField] private Color volumeColor = Color.cyan;

    private readonly List<OVRScenePlane> _scenePlanes = new List<OVRScenePlane>();
    private readonly List<OVRSceneVolume> _sceneVolumes = new List<OVRSceneVolume>();

    private bool _visible = false;
    private readonly Vector3[] _linePoints = new Vector3[4];

    private void Awake()
    {
        if (sceneManager != null)
        {
            sceneManager.SceneModelLoadedSuccessfully += OnSceneModelLoadedSuccessfully;
        }
        DebugLogPanelControls.DebugMenuEvent += DebugMenuToggle;
    }

    private void OnDestroy()
    {
        if (sceneManager != null)
        {
            sceneManager.SceneModelLoadedSuccessfully -= OnSceneModelLoadedSuccessfully;
        }
        DebugLogPanelControls.DebugMenuEvent -= DebugMenuToggle;
    }

    private void Update()
    {
        if (!_visible)
        {
            return;
        }

        var count = _scenePlanes.Count;
        for (int i = 0; i < count; i++)
        {
            DebugDraw(_scenePlanes[i]);
        }

        count = _sceneVolumes.Count;
        for (int i = 0; i < count; i++)
        {
            DebugDraw(_sceneVolumes[i]);
        }
    }

    private void DebugDraw(OVRSceneVolume sceneVolume)
    {
        var volumeTransform = sceneVolume.transform;
        var dimensions = sceneVolume.Dimensions;
        var pos = volumeTransform.position;

        pos.y -=  dimensions.z * 0.5f;

        if (sceneVolume.OffsetChildren)
        {
            var offset = sceneVolume.Offset;
            (offset.x, offset.y, offset.z) = (-offset.x, offset.z, offset.y);

            pos += offset;
        }

        XRGizmos.DrawWireCube(pos, volumeTransform.rotation, dimensions, volumeColor);
    }

    private void DebugDraw(OVRScenePlane scenePlane)
    {
        var planeTransform = scenePlane.transform;
        var halfDim = scenePlane.Dimensions * 0.5f;

        _linePoints[0] = planeTransform.TransformPoint(new Vector2(halfDim.x, halfDim.y));
        _linePoints[1] = planeTransform.TransformPoint(new Vector2(halfDim.x, -halfDim.y));
        _linePoints[2] = planeTransform.TransformPoint(new Vector2(-halfDim.x, -halfDim.y));
        _linePoints[3] = planeTransform.TransformPoint(new Vector2(-halfDim.x, halfDim.y));

        XRGizmos.DrawLineList(_linePoints, planeColor, true);
    }

    private void DebugMenuToggle(bool visible)
    {
        _visible = visible;
    }

    private void OnSceneModelLoadedSuccessfully()
    {
        StartCoroutine(FindSceneComponents());
    }

    public void StaticSceneModelLoaded()
    {
        OnSceneModelLoadedSuccessfully();
    }

    private IEnumerator FindSceneComponents()
    {
        var sceneRoom = FindObjectOfType<OVRSceneRoom>();
        Assert.IsNotNull(sceneRoom);

        while (sceneRoom.Walls.Length == 0)
        {
            yield return null;
        }

        var children = sceneRoom.GetComponentsInChildren<OVRSemanticClassification>(true);

        foreach (var child in children)
        {
            var classification = child.Labels[0];
            if (classification == OVRSceneManager.Classification.Floor ||
                classification == OVRSceneManager.Classification.Ceiling)
            {
                continue;
            }

            if (child.TryGetComponent<OVRSceneVolume>(out var volume))
            {
                _sceneVolumes.Add(volume);
            }
            else if (child.TryGetComponent<OVRScenePlane>(out var plane))
            {
                _scenePlanes.Add(plane);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (sceneManager == null)
        {
            sceneManager = GetComponent<OVRSceneManager>();
        }
    }
#endif
}
