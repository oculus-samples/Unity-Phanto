// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;

/// <summary>
/// Finds the camera rig from the scene
/// </summary>
[MetaCodeSample("Phanto")]
[RequireComponent(typeof(Canvas))]
public class GUICameraFinder : MonoBehaviour
{
    private void Start()
    {
        var cameraRig = FindFirstObjectByType<OVRCameraRig>();
        if (cameraRig)
        {
            var canvas = GetComponent<Canvas>();
            canvas.worldCamera = cameraRig.centerEyeAnchor.GetComponent<Camera>();
            canvas.planeDistance = 0.5f;
        }
        else
        {
            Debug.LogError("No OVRCameraRig found in your scene.");
        }
    }
}
