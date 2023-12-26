// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Phanto.Enemies.DebugScripts;
using Phantom.Environment.Scripts;
using PhantoUtils;
using PhantoUtils.VR;
using UnityEngine;

public class PhantoDemoController : MonoBehaviour
{
    [SerializeField] private Transform phanto;

    [SerializeField] private bool debugDraw = true;

    private Bounds? _bounds;

    private void Awake()
    {
        SceneBoundsChecker.BoundsChanged += OnBoundsChanged;

        DebugDrawManager.DebugDraw = debugDraw;
    }

    private void OnDestroy()
    {
        SceneBoundsChecker.BoundsChanged -= OnBoundsChanged;
    }

    private void OnBoundsChanged(Bounds bounds)
    {
        _bounds = bounds;
    }

    public void PositionAndEnablePhanto(Transform sceneRoot)
    {
        Debug.Log(sceneRoot.name, sceneRoot);

        StartCoroutine(FindASpawnPosition());
    }

    private IEnumerator FindASpawnPosition()
    {
        while (!_bounds.HasValue) yield return null;

        var bounds = _bounds.Value;

        bounds.Expand(-0.5f);

        var head = CameraRig.Instance.CenterEyeAnchor;

        Vector3 spawnPoint = default;
        var attempts = 0;

        // if you're in a small room there may not be space for you and phanto.
        while (attempts++ < 100)
        {
            spawnPoint = bounds.RandomPoint();

            if (Vector3.Distance(head.position, spawnPoint) > 1.0f) break;
        }

        phanto.position = spawnPoint;
        phanto.gameObject.SetActive(true);
    }
}
