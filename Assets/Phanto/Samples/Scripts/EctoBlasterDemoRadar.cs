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
        [Tooltip("The blaster transform")] [SerializeField]
        private Transform blasterBaseTransform;

        [Tooltip("The rotation speed to rotate to the desired target")] [SerializeField]
        private float rotationSpeed = 0.5f;

        [Tooltip("Time to perform scan loop for targets")] [SerializeField]
        private float scanTime = 1.0f;

        private Transform _neckTransform;

        public float TrackingRadius { get; set; } = 0.5f;

        private void Awake()
        {
            Assert.IsNotNull(blasterBaseTransform);
            _neckTransform = blasterBaseTransform.parent;
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
                if (Vector3.Distance(blasterBaseTransform.position, target.position) < TrackingRadius)
                    yield return StartCoroutine(RotateToDirection(target.position, rotationSpeed));

                yield return new WaitForSeconds(scanTime);
            }
        }

        /// <summary>
        ///     Rotates the turret to the desired target
        /// </summary>
        /// <param name="positionToLook">Target position to look at</param>
        /// <param name="timeToRotate">Desired movement time</param>
        /// <returns></returns>
        private IEnumerator RotateToDirection(Vector3 positionToLook,
            float timeToRotate)
        {
            var startRotation = blasterBaseTransform.rotation;
            var direction = positionToLook - blasterBaseTransform.position;
            var finalRotation = Quaternion.LookRotation(direction);

            var neckStartRotation = _neckTransform.rotation;
            var up = transform.up;
            var neckDirection = Vector3.ProjectOnPlane(direction, up).normalized;
            var neckFinalRotation = Quaternion.LookRotation(neckDirection, up);

            var time = 0f;
            while (time <= 1f)
            {
                time += Time.deltaTime / timeToRotate;
                blasterBaseTransform.rotation = Quaternion.Lerp(startRotation, finalRotation, time);
                _neckTransform.rotation = Quaternion.Lerp(neckStartRotation, neckFinalRotation, time);
                yield return null;
            }

            blasterBaseTransform.rotation = finalRotation;
            _neckTransform.rotation = neckFinalRotation;
        }
    }
}
