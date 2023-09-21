// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using Phantom.Environment.Scripts;
using UnityEngine;
using UnityEngine.Assertions;

public class InsideSceneChecker : MonoBehaviour
{
    private const float MaxCeilingRayLength = 100.0f;

    public static event Action<Bounds, bool> UserInSceneChanged;
    public static bool UserInScene { get; private set; }
    private static Bounds _bounds;

    [SerializeField] private SceneBoundsChecker sceneBoundsChecker;
    [SerializeField] private OVRCameraRig cameraRig;

    private bool _boundsSet;

    private void Awake()
    {
        Assert.IsNotNull(sceneBoundsChecker);
        Assert.IsNotNull(cameraRig);
    }

    private void OnEnable()
    {
        SceneBoundsChecker.BoundsChanged += OnSceneBoundsChanged;

        StartCoroutine(UserBoundsTest());
    }

    private void OnDisable()
    {
        SceneBoundsChecker.BoundsChanged -= OnSceneBoundsChanged;
        _boundsSet = false;
    }

    private void OnSceneBoundsChanged(Bounds bounds)
    {
        _bounds = bounds;
        _boundsSet = true;
    }

    private IEnumerator UserBoundsTest()
    {
        // check 5 times a second.
        var wait = new WaitForSeconds(0.2f);

        while (!_boundsSet)
        {
            yield return null;
        }

        var head = cameraRig.centerEyeAnchor;

        while (enabled)
        {
            var inBounds = PointInsideScene(head.position);

            if (UserInScene != inBounds)
            {
                UserInScene = inBounds;
                UserInSceneChanged?.Invoke(_bounds, inBounds);
            }

            yield return wait;
        }
    }

    public static bool PointInsideScene(Vector3 position)
    {
        var inBounds = true;

        if (!_bounds.Contains(position))
        {
            inBounds = false;
        }
        else // if you are inside the axis-aligned bounding box you could still be outside the scene (e.g. L-shaped room).
        {
            // cast ray upwards against scene mesh.
            // if you didn't hit anything you're outside the scene
            // if you hit something and the normal is facing away from you, you're under the scene (basement)
            if (!Physics.Raycast(position, Vector3.up, out var hit, MaxCeilingRayLength, NavMeshConstants.SceneMeshLayerMask)
                || Vector3.Dot(hit.normal, position - hit.point) < 0)
            {
                inBounds = false;
            }
        }

        return inBounds;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (sceneBoundsChecker == null)
        {
            sceneBoundsChecker = GetComponent<SceneBoundsChecker>();
        }

        if (cameraRig == null)
        {
            cameraRig = GetComponentInChildren<OVRCameraRig>(true);
        }
    }
#endif
}
