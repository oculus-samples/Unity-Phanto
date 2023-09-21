// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Phanto.Audio.Scripts
{
    /// <summary>
    /// MonoBehavior attached to GameObjects whose AudioSource component has a
    /// PhantoPolterblastSfxBehavior attached
    /// </summary>
    public class PhantoPolterblastSfxBehavior : PhantoLoopSfxBehavior
    {
        public AnimationCurve distortionResponseCurve;
        public float distortionSmoothingFactor = 3;
        public AudioDistortionFilter distortion;
        public float smoothingFactorUp = 5f;
        public float smoothingFactorDown = 2f;
        private float _currentValue;
        private Vector3 _previousPosition;

        private Transform _transform;


        private void Start()
        {
            _transform = GetComponent<Transform>();
            _previousPosition = _transform.position;
        }

        private void Update()
        {
            if (distortion == null) return;

            _currentValue = InterpolateFloat(_currentValue, CalculateSpeed(), smoothingFactorUp, smoothingFactorDown);
            distortion.distortionLevel = distortionResponseCurve.Evaluate(_currentValue);
        }

        /// <summary>
        /// Calculates the speed of the distortion based on the distance traveled.
        /// </summary>
        private float CalculateSpeed()
        {
            var currentPosition = _transform.position;
            var distanceMoved = Vector3.Distance(_previousPosition, currentPosition);
            var speed = distanceMoved / Time.deltaTime;

            _previousPosition = currentPosition;
            return speed;
        }

        /// <summary>
        /// Calculates the value of a linear interpolation between a and b.
        /// </summary>
        private float InterpolateFloat(float currentValue, float targetValue, float smoothingFactorUp,
            float smoothingFactorDown)
        {
            var smoothingFactor = targetValue > currentValue ? smoothingFactorUp : smoothingFactorDown;
            return Mathf.Lerp(currentValue, targetValue, Time.deltaTime * smoothingFactor);
        }
    }
}
