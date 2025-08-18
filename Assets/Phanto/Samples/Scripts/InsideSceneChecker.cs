// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Collections;
using Phantom.Environment.Scripts;
using UnityEngine;
using UnityEngine.Assertions;

public class InsideSceneChecker : MonoBehaviour
{
    private const float MaxCeilingRayLength = 100.0f;

    public static event Action<bool> UserInSceneChanged;
    public static bool UserInScene { get; private set; }

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
        SceneBoundsChecker.WorldAligned += OnWorldAligned;

        StartCoroutine(UserBoundsTest());
    }

    private void OnDisable()
    {
        SceneBoundsChecker.WorldAligned -= OnWorldAligned;
        _boundsSet = false;
    }

    private void OnWorldAligned()
    {
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
                UserInSceneChanged?.Invoke(inBounds);
            }

            yield return wait;
        }
    }

    public static bool PointInsideScene(Vector3 position)
    {
        return SceneQuery.TryGetRoomContainingPoint(position, out _);
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
