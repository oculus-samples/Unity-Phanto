// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using PhantoUtils;
using UnityEngine;

namespace Phantom.Environment.Scripts
{
    /// <summary>
    ///     Represents a scene anchor, holding relevant information.
    /// </summary>
    public class PhantoAnchorInfo : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Transform _transform;
        [SerializeField, HideInInspector] private GameObject _gameObject;

        [SerializeField, HideInInspector] private OVRSceneAnchor _sceneAnchor;

        private OVRSemanticClassification _semanticClassification;
        private OVRSceneVolume _sceneVolume;
        private OVRScenePlane _scenePlane;
        private OVRSceneVolumeMeshFilter _sceneVolumeMeshFilter;
        private OVRScenePlaneMeshFilter _scenePlaneMeshFilter;
        private OVRSceneRoom _owningRoom;

        public bool IsVolume => _hasVolume || _hasVolumeMeshFilter;
        public bool IsPlane => _hasPlane || _hasPlaneMeshFilter;

        private bool _hasVolume;
        private bool _hasVolumeMeshFilter;
        private bool _hasPlane;
        private bool _hasPlaneMeshFilter;

        public Vector3 Position => _transform.position;
        public Pose Pose => new Pose(_transform.position, _transform.rotation);

        public string Classification => _semanticClassification.Labels[0];

        public Vector3 Dimensions
        {
            get
            {
                if (_hasVolume)
                {
                    return _sceneVolume.Dimensions;
                }

                if (_hasPlane)
                {
                    return _scenePlane.Dimensions;
                }

                return default;
            }
        }

        public float Width
        {
            get
            {
                if (_hasVolume)
                {
                    return _sceneVolume.Width;
                }

                if (_hasPlane)
                {
                    return _scenePlane.Width;
                }

                return default;
            }
        }

        public float Height
        {
            get
            {
                if (_hasVolume)
                {
                    return _sceneVolume.Height;
                }

                if (_hasPlane)
                {
                    return _scenePlane.Height;
                }

                return default;
            }
        }

        public float Depth
        {
            get
            {
                if (_hasVolume)
                {
                    return _sceneVolume.Depth;
                }

                return default;
            }
        }

        public Vector3 Forward => _transform.forward;

        private void Awake()
        {
            FindDependencies();
        }

        private IEnumerator Start()
        {
            while (!_sceneAnchor.Space.Valid) yield return null;

            _owningRoom = GetComponentInParent<OVRSceneRoom>(true);

            _hasVolume = TryGetComponent(out _sceneVolume);
            _hasVolumeMeshFilter = TryGetComponent(out _sceneVolumeMeshFilter);

            _hasPlane = TryGetComponent(out _scenePlane);
            _hasPlaneMeshFilter = TryGetComponent(out _scenePlaneMeshFilter);

            while (!TryGetComponent(out _semanticClassification)) yield return null;

            var handle = (ushort)_sceneAnchor.Space.Handle;

            _gameObject.SetSuffix($"{Classification}_{handle:X4}_{(IsVolume ? "V" : "P")}");
        }

        private void FindDependencies()
        {
            _transform = transform;
            _gameObject = gameObject;

            if (_sceneAnchor == null)
            {
                _sceneAnchor = GetComponent<OVRSceneAnchor>();
            }
        }

        public bool Contains(string comparison)
        {
            return _semanticClassification.Contains(comparison);
        }

        public bool ContainsAny(IEnumerable<string> comparison)
        {
            foreach (var item in comparison)
            {
                if (_semanticClassification.Contains(item))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Projects point onto plane and returns signed distance to point.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public Vector3 ClosestPointOnPlane(Vector3 point, out float distance)
        {
            Plane plane;

            if (_hasPlane || _hasVolume)
            {
                plane = new Plane(_transform.forward, _transform.position);
            }
            else
            {
                distance = float.PositiveInfinity;
                return Vector3.positiveInfinity;
            }

            distance = plane.GetDistanceToPoint(point);
            return plane.ClosestPointOnPlane(point);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            FindDependencies();
        }
#endif

        /// <summary>
        /// Projects world space point onto OVRScenePlane and
        /// returns whether that point is within the bounds of the OVRScenePlane.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="distance">distance to plane will be negative if point is behind plane</param>
        /// <returns></returns>
        public bool PlaneContainsPoint(Vector3 point, out float distance)
        {
            if (!_hasPlane)
            {
                distance = float.PositiveInfinity;
                return false;
            }

            var projectedPoint = ClosestPointOnPlane(point, out distance);

            var localPoint = _transform.InverseTransformPoint(projectedPoint);
            return SceneQuery.PointInPolygon2D(_scenePlane.Boundary, localPoint);
        }
    }
}
