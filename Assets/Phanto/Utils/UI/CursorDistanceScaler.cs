// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
using UnityEngine.Assertions;

namespace PhantoUtils.VR
{
    public class CursorDistanceScaler : MonoBehaviour
    {
        [SerializeField] private float minScale = 0.025f;
        [SerializeField] private float maxScale = 0.2f;

        [SerializeField] private float minDistance = 1.0f;
        [SerializeField] private float maxDistance = 5.0f;

        private void Update()
        {
            Assert.IsNotNull(CameraRig.Instance, $"{nameof(CameraRig.Instance)} cannot be null.");
            if (CameraRig.Instance == null) enabled = false;

            var distanceAmount = Vector3.Distance(transform.position, CameraRig.Instance.CenterEyeAnchor.position);
            distanceAmount = Mathf.Clamp01((distanceAmount - minDistance) / maxDistance);
            var scale = minScale + distanceAmount * (maxScale - minScale);
            transform.localScale = Vector3.one * scale;
        }
    }
}
