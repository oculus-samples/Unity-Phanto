// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Phantom.Environment.Scripts
{
    /// <summary>
    ///     This script checks the bounds of the scene against the floor
    /// </summary>
    public class SceneBoundsChecker : MonoBehaviour
    {
        private static Bounds? _bounds;
        [SerializeField] private OVRCameraRig cameraRig;

        [SerializeField] [Tooltip("Amount the floor must shift to trigger realignment.")]
        private float maxShift = 0.05f;

        [SerializeField] [Tooltip("Amount the floor must rotate to trigger realignment.")]
        private float maxRotation = 5f;

        [SerializeField] [Tooltip("Rotate the tracking space to make sure the room's floor is 0,0,0 and axis aligned.")]
        private bool axisAlignFloor = true;

        private bool _boundsDirty;

        private Coroutine _boundsPollingCoroutine;
        private OVRScenePlane _floorPlane;

        private Transform _floorTransform;

        private OVRSceneRoom _room;
        private Transform _trackingSpaceTransform;

        public Bounds Bounds => _bounds.GetValueOrDefault();

        private static event Action<Bounds> _boundsChanged;

        public static event Action<Bounds> BoundsChanged
        {
            add
            {
                if (_bounds.HasValue) value?.Invoke(_bounds.Value);

                _boundsChanged += value;
            }
            remove => _boundsChanged -= value;
        }

        private bool IsFloorAligned
        {
            get
            {
                if (!_floorPlane.enabled) return true;

                var angle = Vector3.Angle(Vector3.forward, _floorTransform.up);

                if (angle > maxRotation) return false;

                var floorPos = Vector3.ProjectOnPlane(_floorTransform.position, Vector3.up);
                var sqrMagnitude = floorPos.sqrMagnitude;

                return sqrMagnitude < maxShift * maxShift;
            }
        }

        private void Awake()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnEnable()
        {
            OVRManager.VrFocusAcquired += StartBoundsPolling;
            OVRManager.VrFocusLost += CancelBoundsPolling;

            StartBoundsPolling();
        }

        private void OnDisable()
        {
            OVRManager.VrFocusAcquired -= StartBoundsPolling;
            OVRManager.VrFocusLost -= CancelBoundsPolling;

            CancelBoundsPolling();
        }

        private void OnDestroy()
        {
            _bounds = null;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        // When user suspends the app to rescan we get an OnApplicationPause event
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                CancelBoundsPolling();
            }
            else
            {
                StartBoundsPolling();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_bounds.HasValue) return;

            var bounds = _bounds.Value;
            Gizmos.color = _boundsDirty ? Color.red : Color.green;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            if (_trackingSpaceTransform != null)
            {
                var ray = new Ray(_trackingSpaceTransform.position, _trackingSpaceTransform.forward);

                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(ray);
            }
        }
#endif

        private void OnActiveSceneChanged(Scene a, Scene b)
        {
            // assume room has been destroyed and find the new one.
            StartBoundsPolling();
        }

        private IEnumerator BoundsChangePolling()
        {
            // wait until room is loaded.
            do
            {
                yield return null;
                _room = FindObjectOfType<OVRSceneRoom>();
            } while (_room == null);

            var timeoutStopwatch = Stopwatch.StartNew();
            while (_room.Walls.Length == 0)
            {
                if (timeoutStopwatch.ElapsedMilliseconds > 3000)
                {
                    Debug.LogWarning("Timed out waiting for walls in scene. Walls missing?");
                    break;
                }
                yield return null;
            }

            var sceneAnchors = _room.GetComponentsInChildren<OVRSceneAnchor>();
            var sceneAnchorCount = sceneAnchors.Length;

            // Wait for every scene anchor's position to get updated.
            for (var i = 0; i < sceneAnchorCount; i++)
            {
                yield return null;
            }

            _trackingSpaceTransform = cameraRig.trackingSpace;

            // OVRSceneRoom can get destroyed while we're waiting.
            if (_room == null || _room.Floor == null)
            {
                CancelBoundsPolling();
                StartBoundsPolling();
                yield break;
            }

            _floorPlane = _room.Floor;
            _floorTransform = _floorPlane.transform;

            Bounds previousBounds = default;
            while (enabled)
            {
                if (axisAlignFloor && !IsFloorAligned) AxisAlignFloor(_floorTransform, _trackingSpaceTransform);

                // Wait for every scene anchor's position to get updated.
                for (var i = 0; i < sceneAnchorCount; i++)
                {
                    yield return null;
                }

                var currentBounds = DetermineBounds(_room);

                var centerDelta = Vector3.Distance(previousBounds.center, currentBounds.center);
                var extentsDelta = Vector3.Distance(previousBounds.extents, currentBounds.extents);

                if (Mathf.Max(centerDelta, extentsDelta) > maxShift)
                {
                    _bounds = previousBounds;
                    previousBounds = currentBounds;
                    _boundsDirty = true;
                    Debug.Log($"[SceneBoundsChecker:{Time.frameCount}] Bounds dirty");
                    continue;
                }

                if (_boundsDirty)
                {
                    _boundsDirty = false;

                    _bounds = currentBounds;
                    _boundsChanged?.Invoke(currentBounds);
                    Debug.Log($"[SceneBoundsChecker:{Time.frameCount}] Bounds finished moving");
                }
            }
        }

        public void RecalculateBounds()
        {
            var bounds = DetermineBounds(_room);
            _boundsChanged?.Invoke(bounds);
            _bounds = bounds;
        }

        private void StartBoundsPolling()
        {
            if (_boundsPollingCoroutine != null) StopCoroutine(_boundsPollingCoroutine);

            _boundsDirty = true;
            _boundsPollingCoroutine = StartCoroutine(BoundsChangePolling());
        }

        private void CancelBoundsPolling()
        {
            if (_boundsPollingCoroutine != null)
            {
                StopCoroutine(_boundsPollingCoroutine);
                _boundsPollingCoroutine = null;
            }
        }

        private static Bounds DetermineBounds(OVRSceneRoom room)
        {
            void EncapsulatePlane(OVRScenePlane plane, ref Bounds bounds)
            {
                var planeTransform = plane.transform;
                var halfDims = (Vector3)plane.Dimensions * 0.5f;

                bounds.Encapsulate(planeTransform.TransformPoint(halfDims));
                bounds.Encapsulate(planeTransform.TransformPoint(-halfDims));
            }

            var bounds = new Bounds(room.Floor.transform.position, Vector3.zero);
            EncapsulatePlane(room.Ceiling, ref bounds);

            foreach (var wall in room.Walls) EncapsulatePlane(wall, ref bounds);

            return bounds;
        }

        /// <summary>
        ///     Move the tracking space so the floor is at 0,~0,0 and axis aligned.
        ///     Should result in tighter room bounds.
        /// </summary>
        /// <param name="floorTransform"></param>
        /// <param name="trackingSpace"></param>
        private static void AxisAlignFloor(Transform floorTransform, Transform trackingSpace)
        {
            // move/rotate the floor to axis aligned 0,0,0.

            // floor's up axis is actually pointing Unity forward.
            var forward = Vector3.ProjectOnPlane(floorTransform.up, Vector3.up).normalized;

            var angle = Vector3.SignedAngle(forward, Vector3.forward, Vector3.up);
            var floorPos = Vector3.ProjectOnPlane(floorTransform.position, Vector3.up);

            // shift the tracking space so the floorPos gets moved to 0,0,0.
            trackingSpace.position -= floorPos;

            // rotate the tracking space around the origin so forward and Vector3.forward are parallel.
            trackingSpace.RotateAround(Vector3.zero, Vector3.up, angle);
        }

        public static bool PointInBounds(Vector3 point)
        {
            return _bounds.HasValue ? _bounds.Value.Contains(point) : false;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SceneBoundsChecker))]
    public class SceneBoundsCheckerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (EditorApplication.isPlaying)
            {
                GUILayout.Space(16);

                if (GUILayout.Button("Recalculate Bounds"))
                {
                    var loader = target as SceneBoundsChecker;
                    loader.RecalculateBounds();
                }
            }
        }
    }
#endif
}
