// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using PhantoUtils.VR;
using UnityEngine;
using UnityEngine.Assertions;

namespace Phantom.EctoBlaster.Scripts
{
    /// <summary>
    ///     Points the turret towards the user if they are in range.
    /// </summary>
    public class EctoBlasterDemoRadar : MonoBehaviour
    {
        [Tooltip("The barrel transform")] [SerializeField]
        private Transform pitchTransform;

        [SerializeField]
        private Transform yawTransform;

        [Tooltip("The rotation speed to rotate to the desired target")] [SerializeField]
        private float rotationSpeed = 0.5f;

        [Tooltip("Time to perform scan loop for targets")] [SerializeField]
        private float scanTime = 1.0f;

        private Transform _transform;

        public float TrackingRadius { get; set; } = 0.5f;

        private void Awake()
        {
            Assert.IsNotNull(pitchTransform);
            Assert.IsNotNull(yawTransform);

            _transform = transform;
        }

        private void OnEnable()
        {
            StartCoroutine(TrackPlayer());
        }

        private IEnumerator TrackPlayer()
        {
            while (CameraRig.Instance == null) yield return null;

            var target = CameraRig.Instance.CenterEyeAnchor;

            while (enabled)
            {
                if (Vector3.Distance(pitchTransform.position, target.position) < TrackingRadius)
                    yield return StartCoroutine(RotateToDirection(target.position, rotationSpeed));

                yield return new WaitForSeconds(scanTime);
            }
        }

        /// <summary>
        ///     Rotates the turret to the desired target
        /// </summary>
        /// <param name="worldLookPosition">Target position to look at</param>
        /// <param name="timeToRotate">Desired movement time</param>
        /// <returns></returns>
        private IEnumerator RotateToDirection(Vector3 worldLookPosition,
            float timeToRotate)
        {
            var baseUp = _transform.up;
            var direction = worldLookPosition - pitchTransform.position;

            var yawRotation = yawTransform.rotation;
            var yawDirection = Vector3.ProjectOnPlane(direction, baseUp).normalized;
            var finalYawRotation = Quaternion.LookRotation(yawDirection, baseUp);

            var startRotation = pitchTransform.localRotation;
            // where will the barrel be pointing after the turret has rotated
            var pitchDirection = Quaternion.Inverse(finalYawRotation) * direction;
            var finalRotation = Quaternion.LookRotation(pitchDirection, Vector3.up);

            var time = 0f;
            while (time <= 1f)
            {
                time += Time.deltaTime / timeToRotate;

                yawTransform.rotation = Quaternion.Lerp(yawRotation, finalYawRotation, time);
                pitchTransform.localRotation = Quaternion.Lerp(startRotation, finalRotation, time);

                yield return null;
            }

            pitchTransform.localRotation = finalRotation;
            yawTransform.rotation = finalYawRotation;
        }
    }
}
