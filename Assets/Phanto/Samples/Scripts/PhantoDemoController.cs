// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using System.Collections;
using Phanto.Enemies.DebugScripts;
using Phantom.Environment.Scripts;
using PhantoUtils;
using PhantoUtils.VR;
using UnityEngine;

[MetaCodeSample("Phanto")]
public class PhantoDemoController : MonoBehaviour
{
    [SerializeField] private Transform phanto;

    [SerializeField] private bool debugDraw = true;

    private bool _sceneReady;

    private void Awake()
    {
        SceneBoundsChecker.WorldAligned += OnBoundsChanged;

        DebugDrawManager.DebugDraw = debugDraw;
    }

    private void OnDestroy()
    {
        SceneBoundsChecker.WorldAligned -= OnBoundsChanged;
    }

    private void OnBoundsChanged()
    {
        _sceneReady = true;
    }

    public void PositionAndEnablePhanto(Transform sceneRoot)
    {
        Debug.Log(sceneRoot.name, sceneRoot);

        StartCoroutine(FindASpawnPosition());
    }

    private IEnumerator FindASpawnPosition()
    {
        while (!_sceneReady) yield return null;

        var head = CameraRig.Instance.CenterEyeAnchor;
        var room = SceneQuery.GetRoomContainingPoint(head.position);

        var bounds = SceneQuery.GetRoomBounds(room);
        bounds.Expand(-0.5f);

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
