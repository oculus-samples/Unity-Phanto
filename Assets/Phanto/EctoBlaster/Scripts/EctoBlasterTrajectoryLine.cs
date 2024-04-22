// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using Phantom.EctoBlaster.Scripts;
using UnityEngine;

/// <summary>
/// This class represents an arc line, to highlight the placement of the blaster
/// </summary>
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EctoBlasterSpawner))]
public class EctoBlasterTrajectoryLine : MonoBehaviour
{
    [Tooltip("Number of points to represent the arc")] [SerializeField]
    private int positionCount = 10;

    [Tooltip("Arc height at center")] [SerializeField]
    private float arcHeight = .2f;

    [Tooltip("Animation curve to control the arc width along the line")] [SerializeField]
    private AnimationCurve arcCurve;

    // Private members
    private LineRenderer _lineRenderer;
    private EctoBlasterSpawner _blasterSpawner;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.enabled = false;
        _blasterSpawner = GetComponent<EctoBlasterSpawner>();
        _lineRenderer.positionCount = positionCount;
        _lineRenderer.widthCurve = arcCurve;
        _blasterSpawner.onBlasterPreview.AddListener(OnNewBlasterHit);
        _blasterSpawner.onBlasterPlaced.AddListener(OnBlasterPlaced);
    }

    private void OnDestroy()
    {
        _blasterSpawner.onBlasterPreview.RemoveListener(OnNewBlasterHit);
        _blasterSpawner.onBlasterPlaced.RemoveListener(OnBlasterPlaced);
    }

    public void Show(bool visible = true)
    {
        _lineRenderer.enabled = visible;
    }

    public void Hide()
    {
        Show(false);
    }

    private void OnBlasterPlaced(RaycastHit arg0)
    {
        _lineRenderer.enabled = false;
    }

    private void OnNewBlasterHit(RaycastHit hit)
    {
        _lineRenderer.enabled = true;

        var step = Vector3.Distance(transform.position, hit.point) / positionCount;

        for (int i = 0; i < positionCount; i++)
        {
            var position = SampleParabola(transform.position, hit.point, arcHeight, step * i, hit.normal);
            _lineRenderer.SetPosition(i, position);
        }
    }

    /// <summary>
    /// Sample points along the arc
    /// </summary>
    /// <param name="start">Start position (controller)</param>
    /// <param name="end">End position (blaster)</param>
    /// <param name="height">Arc height</param>
    /// <param name="t">Position along the arc</param>
    /// <param name="outDirection">Up direction</param>
    /// <returns></returns>
    private Vector3 SampleParabola(Vector3 start, Vector3 end, float height, float t, Vector3 outDirection)
    {
        float parabolicT = t * 2 - 1;
        Vector3 travelDirection = end - start;
        Vector3 up = outDirection;
        Vector3 result = start + t * travelDirection;
        result += ((-parabolicT * parabolicT + 1) * height) * up.normalized;
        return result;
    }
}
