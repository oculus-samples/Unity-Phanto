// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using Common;
using Meta.XR.MRUtilityKit;
using UnityEngine;
using UnityEngine.Assertions;
using Utilities.XR;

[RequireComponent(typeof(MRUK))]
public class DebugSceneEntities : MonoBehaviour
{

    [SerializeField] private Color planeColor = Color.yellow;
    [SerializeField] private Color volumeColor = Color.cyan;

    private readonly List<MRUKAnchor> _scenePlanes = new List<MRUKAnchor>();
    private readonly List<MRUKAnchor> _sceneVolumes = new List<MRUKAnchor>();

    private bool _visible = false;
    private readonly Vector3[] _linePoints = new Vector3[256];

    private void Awake()
    {
        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.AddListener(OnSceneModelLoadedSuccessfully);
        }
        DebugLogPanelControls.DebugMenuEvent += DebugMenuToggle;
    }

    private void OnDestroy()
    {
        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.RemoveListener(OnSceneModelLoadedSuccessfully);
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
            DebugDrawPlane(_scenePlanes[i]);
        }

        count = _sceneVolumes.Count;
        for (int i = 0; i < count; i++)
        {
            DebugDrawVolume(_sceneVolumes[i]);
        }
    }

    private void DebugDrawVolume(MRUKAnchor sceneVolume)
    {
        if (!sceneVolume.gameObject.activeInHierarchy)
        {
            return;
        }

        var volumeTransform = sceneVolume.transform;
        var dimensions = sceneVolume.VolumeBounds.Value.size;
        var pos = volumeTransform.position;

        pos.y -= dimensions.z * 0.5f;

        XRGizmos.DrawWireCube(pos, volumeTransform.rotation, dimensions, volumeColor);
    }

    private void DebugDrawPlane(MRUKAnchor scenePlane)
    {
        if (!scenePlane.gameObject.activeInHierarchy)
        {
            return;
        }

        var planeTransform = scenePlane.transform;

        var pointCount = Mathf.Min(scenePlane.PlaneBoundary2D.Count, _linePoints.Length);

        for (int i = 0; i < pointCount; i++)
        {
            _linePoints[i] = planeTransform.TransformPoint(scenePlane.PlaneBoundary2D[i]);
        }

        XRGizmos.DrawLineList(_linePoints, planeColor, true, pointCount);
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
        var sceneRoom = FindFirstObjectByType<MRUKRoom>();
        Assert.IsNotNull(sceneRoom);

        while (sceneRoom.WallAnchors.Count == 0)
        {
            yield return null;
        }

        var children = sceneRoom.GetComponentsInChildren<OVRSemanticClassification>(true);

        foreach (var child in children)
        {
            if (child.TryGetComponent<MRUKAnchor>(out var volume))
            {
                _sceneVolumes.Add(volume);
            }
            else if (child.TryGetComponent<MRUKAnchor>(out var plane))
            {
                _scenePlanes.Add(plane);
            }
        }
    }
}
