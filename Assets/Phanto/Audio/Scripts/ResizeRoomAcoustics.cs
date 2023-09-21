// Copyright (c) Meta Platforms, Inc. and affiliates.

using Phanto.Enemies.DebugScripts;
using Phantom.Environment.Scripts;
using PhantoUtils;
using UnityEngine;
using Utilities.XR;

namespace Phanto.Audio.Scripts
{
    /// <summary>
    ///     This script resizes the room acoustic properties based on the room bound
    /// </summary>
    public class ResizeRoomAcoustics : MonoBehaviour
    {
        [SerializeField] private MetaXRAudioRoomAcousticProperties roomAcousticProperties;
        [SerializeField] private bool dontDestroyOnLoad;
        [SerializeField] private bool showDebugRoomBounds;

        private Bounds? _debugBounds;

        private void Awake()
        {
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneBoundsChecker.BoundsChanged += OnBoundsChanged;

            if (showDebugRoomBounds) DebugDrawManager.DebugDrawEvent += DebugDraw;
        }

        private void OnDisable()
        {
            SceneBoundsChecker.BoundsChanged -= OnBoundsChanged;

            DebugDrawManager.DebugDrawEvent -= DebugDraw;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (roomAcousticProperties == null)
                roomAcousticProperties = GetComponent<MetaXRAudioRoomAcousticProperties>();
        }
#endif

        private void OnBoundsChanged(Bounds bounds)
        {
            _debugBounds = bounds;
            transform.position = bounds.center;
            var size = bounds.size;
            roomAcousticProperties.height = size.y;
            roomAcousticProperties.width = size.x;
            roomAcousticProperties.depth = size.z;
        }

        private void DebugDraw()
        {
            if (!_debugBounds.HasValue) return;

            var bounds = _debugBounds.Value;
            XRGizmos.DrawWireCube(bounds.center, Quaternion.identity, bounds.size, MSPalette.DodgerBlue, 0.008f);
        }
    }
}
