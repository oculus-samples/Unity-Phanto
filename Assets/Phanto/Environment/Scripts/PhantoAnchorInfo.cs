// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using System;
using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using PhantoUtils;
using UnityEngine;

namespace Phantom.Environment.Scripts
{
    /// <summary>
    ///     Represents a scene anchor, holding relevant information.
    /// </summary>
    [MetaCodeSample("Phanto")]
    public class PhantoAnchorInfo : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Transform _transform;
        [SerializeField, HideInInspector] private GameObject _gameObject;

        [SerializeField, HideInInspector] private MRUKAnchor _sceneAnchor;

        private MRUKAnchor _semanticClassification;
        private MRUKRoom _owningRoom;

        public bool IsVolume => _hasVolume;
        public bool IsPlane => _hasPlane;

        private bool _hasVolume;
        private bool _hasPlane;

        public Vector3 Position => _transform.position;
        public Pose Pose => new Pose(_transform.position, _transform.rotation);

        public string Classification => _semanticClassification.Label.ToString();

        public Vector3 Dimensions
        {
            get
            {
                if (_hasVolume)
                {
                    return _semanticClassification.VolumeBounds.Value.size;
                }

                if (_hasPlane)
                {
                    return _semanticClassification.PlaneRect.Value.size;
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
                    return _semanticClassification.VolumeBounds.Value.size.x;
                }

                if (_hasPlane)
                {
                    return _semanticClassification.PlaneRect.Value.width;
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
                    return _semanticClassification.VolumeBounds.Value.size.y;
                }

                if (_hasPlane)
                {
                    return _semanticClassification.PlaneRect.Value.height;
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
                    return _semanticClassification.VolumeBounds.Value.size.z;
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
            _owningRoom = GetComponentInParent<MRUKRoom>(true);
            while (!TryGetComponent(out _semanticClassification)) yield return null;

            _hasVolume = _semanticClassification.VolumeBounds.HasValue;

            _hasPlane = _semanticClassification.PlaneRect.HasValue;

            _gameObject.SetSuffix($"{Classification}_{(IsVolume ? "V" : "P")}");
        }

        private void FindDependencies()
        {
            _transform = transform;
            _gameObject = gameObject;

            if (_sceneAnchor == null)
            {
                _sceneAnchor = GetComponent<MRUKAnchor>();
            }

            if (_sceneAnchor == null)
            {
                _sceneAnchor = GetComponentInParent<MRUKAnchor>();
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
                if (_semanticClassification != null && _semanticClassification.ToString().Contains(item))
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
        /// Projects world space point onto MRUKAnchor and
        /// returns whether that point is within the bounds of the MRUKAnchor.
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
            return SceneQuery.PointInPolygon2D(_semanticClassification.PlaneBoundary2D, localPoint);
        }
    }
}
