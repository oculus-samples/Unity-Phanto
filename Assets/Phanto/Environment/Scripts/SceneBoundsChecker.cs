// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Meta.XR.MRUtilityKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Phantom.Environment.Scripts
{
    /// <summary>
    ///     This script checks the bounds of the scene against the floor
    /// </summary>
    [MetaCodeSample("Phanto")]
    public class SceneBoundsChecker : MonoBehaviour
    {
        private static Bounds? _currentBounds;
        private static MRUKRoom _currentRoom;

        [SerializeField] private OVRCameraRig cameraRig;

        [SerializeField]
        [Tooltip("Amount the floor must shift to trigger realignment.")]
        private float maxShift = 0.05f;

        [SerializeField]
        [Tooltip("Amount the floor must rotate to trigger realignment.")]
        private float maxRotation = 5f;

        [SerializeField]
        [Tooltip("Rotate the tracking space to make sure the room's floor is 0,0,0 and axis aligned.")]
        private bool axisAlignFloor = true;

        private Coroutine _boundsPollingCoroutine;
        private MRUKAnchor _floorPlane;

        private Transform _floorTransform;
        private Transform _trackingSpaceTransform;
        private Transform _headTransform;

        private static event Action<MRUKRoom, Bounds> _boundsChanged;
        /// <summary>
        /// The user has moved from one room to another.
        /// </summary>
        public static event Action<MRUKRoom, Bounds> BoundsChanged
        {
            add
            {
                if (_currentBounds.HasValue) value?.Invoke(_currentRoom, _currentBounds.Value);

                _boundsChanged += value;
            }
            remove => _boundsChanged -= value;
        }

        private static event Action _worldAligned;
        /// <summary>
        /// If "axis align floor" is true this will be invoked when we've moved
        /// the user's first room to the origin.
        /// </summary>
        public static event Action WorldAligned
        {
            add
            {
                if (_currentBounds.HasValue) value?.Invoke();

                _worldAligned += value;
            }
            remove => _worldAligned -= value;
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
            _headTransform = cameraRig.centerEyeAnchor;

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
            _currentBounds = null;
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
            if (!_currentBounds.HasValue) return;

            var bounds = _currentBounds.Value;
            Gizmos.color = Color.green;
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
            _currentRoom = null;
            var wait = new WaitForSeconds(0.25f);
            MRUKRoom room = null;

            // wait until room is loaded.
            do
            {
                yield return null;
            } while (!SceneQuery.TryGetRoomContainingPoint(cameraRig.centerEyeAnchor.position, out room));

            var timeoutStopwatch = Stopwatch.StartNew();
            while (room.FloorAnchor == null || room.FloorAnchor.PlaneBoundary2D.Count == 0 || room.WallAnchors.Count == 0)
            {
                if (timeoutStopwatch.ElapsedMilliseconds > 3000)
                {
                    Debug.LogWarning("Timed out waiting for walls in scene. Walls missing?");
                    break;
                }

                yield return null;
            }

            _trackingSpaceTransform = cameraRig.trackingSpace;

            // MRUKRoom can get destroyed while we're waiting.
            if (room == null || room.FloorAnchor == null)
            {
                CancelBoundsPolling();
                StartBoundsPolling();
                yield break;
            }

            _floorPlane = room.FloorAnchor;
            _floorTransform = _floorPlane.transform;

            if (!axisAlignFloor || IsFloorAligned)
            {
                _currentBounds = default;
                _worldAligned?.Invoke();
            }

            while (enabled)
            {
                if (axisAlignFloor && !IsFloorAligned) AxisAlignFloor(_floorTransform, _trackingSpaceTransform);

                // find the room the user is currently in.
                var headRoom = SceneQuery.GetRoomContainingPoint(_headTransform.position);

                // if it is not the same as the current room
                if (headRoom != _currentRoom)
                {
                    // calculate bounds and broadcast event.
                    _currentRoom = headRoom;
                    _currentBounds = SceneQuery.GetRoomBounds(headRoom);

                    _boundsChanged?.Invoke(_currentRoom, _currentBounds.Value);
                }

                yield return wait;
            }
        }

        public void RecalculateBounds()
        {
            // get the room the user's camera is in.
            var room = SceneQuery.GetRoomContainingPoint(cameraRig.centerEyeAnchor.position);

            var bounds = SceneQuery.GetRoomBounds(room);
            _boundsChanged?.Invoke(room, bounds);
            _currentBounds = bounds;
        }

        private void StartBoundsPolling()
        {
            if (_boundsPollingCoroutine != null) StopCoroutine(_boundsPollingCoroutine);

            _currentRoom = null;
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

        /// <summary>
        ///     Move the tracking space so the floor is at 0,~0,0 and axis aligned.
        ///     Should result in tighter room bounds.
        /// </summary>
        /// <param name="floorTransform"></param>
        /// <param name="trackingSpace"></param>
        private static void AxisAlignFloor(Transform floorTransform, Transform trackingSpace)
        {
            // floor's up axis is actually pointing Unity forward.
            var forward = Vector3.ProjectOnPlane(floorTransform.up, Vector3.up).normalized;

            var angle = Vector3.SignedAngle(forward, Vector3.forward, Vector3.up);
            var floorPos = Vector3.ProjectOnPlane(floorTransform.position, Vector3.up);

            // shift the tracking space so the floorPos gets moved to 0,0,0.
            trackingSpace.position -= floorPos;

            // rotate the tracking space around the origin so forward and Vector3.forward are parallel.
            trackingSpace.RotateAround(Vector3.zero, Vector3.up, angle);

            _worldAligned?.Invoke();
        }

        public static bool PointInBounds(Vector3 point)
        {
            return _currentBounds.HasValue ? _currentBounds.Value.Contains(point) : false;
        }
    }

#if UNITY_EDITOR
    [MetaCodeSample("Phanto")]
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
