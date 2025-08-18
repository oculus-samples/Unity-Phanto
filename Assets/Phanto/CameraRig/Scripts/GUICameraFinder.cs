// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEngine;

/// <summary>
/// Finds the camera rig from the scene
/// </summary>
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
