// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Phantom.EctoBlaster.Scripts
{
    /// <summary>
    ///     Handles discovery and targeting of the desired targets
    /// </summary>
    public class EctoBlasterRadar : MonoBehaviour
    {
        [Tooltip("The blaster transform")] [SerializeField]
        private Transform baseTransform;

        [SerializeField]
        private Transform pitchTransform;

        [SerializeField]
        private Transform yawTransform;

        [Tooltip("The rotation speed to rotate to the desired target")] [SerializeField]
        private float rotationSpeed = 0.5f;

        [Tooltip("Time to perform scan loop for targets")] [SerializeField]
        private float scanTime = 1.0f;

        [SerializeField] private PhantomFleeTarget _fleeTarget;

        private float destroyTime = 8f;

        private PolterblastTrigger _blasterTrigger;

        private IReadOnlyCollection<PhantomController> _targets;

        private Transform _transform;

        public float BlastRadius { get; set; } = 0.5f;

        public float DestroyTime
        {
            get => destroyTime;
            set => destroyTime = value;
        }

        private void Awake()
        {
            _transform = transform;

            Assert.IsNotNull(_fleeTarget);
        }

        private void OnEnable()
        {
            _fleeTarget.SetPositionAndDirection(_transform.position, Vector3.zero);
            _fleeTarget.Show();
        }

        private void OnDisable()
        {
            _fleeTarget.Hide();
        }

        private IEnumerator Start()
        {
            Destroy(gameObject, destroyTime);

            _blasterTrigger = GetComponentInChildren<PolterblastTrigger>(true);
            Assert.IsNotNull(_blasterTrigger);
            Assert.IsNotNull(baseTransform);

            yield return null;

            while (true)
            {
                //Locate and update targets
                _targets = PhantomManager.Instance.ActivePhantoms;
                if (_targets.Count == 0) yield return new WaitForSeconds(scanTime);

                //Find best target and check range
                var target = FindTarget(_targets);
                var targetFound = target != null &&
                                  Vector3.Distance(baseTransform.position, target.HeadPosition) < BlastRadius;

                //Ecto blaster if a target was found and start rotation
                _blasterTrigger.AutomaticEnabled = targetFound;
                if (targetFound)
                    yield return StartCoroutine(RotateToDirection(target.HeadPosition,
                        rotationSpeed));

                yield return new WaitForSeconds(scanTime);
            }
        }

        /// <summary>
        ///     Locates the desired target, in this case, by distance
        /// </summary>
        /// <param name="targetTransforms">List of target transforms</param>
        /// <returns></returns>
        private T FindTarget<T>(IEnumerable<T> targets) where T : MonoBehaviour
        {
            return FindClosestObject(targets, baseTransform.position);
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

        /// <summary>
        ///     Finds the closest transform to desired target position
        /// </summary>
        /// <param name="phantoms">a collection of phantoms</param>
        /// <param name="targetPosition">Vector3 of the position to compare to</param>
        /// <returns></returns>
        private static T FindClosestObject<T>(IEnumerable<T> phantoms, Vector3 targetPosition) where T : MonoBehaviour
        {
            T closestObject = null;
            var closestDistance = float.MaxValue;

            foreach (var obj in phantoms)
            {
                var tform = obj.transform;
                var distance = Vector3.Distance(tform.position, targetPosition);

                if (!(distance < closestDistance)) continue;

                closestObject = obj;
                closestDistance = distance;
            }

            return closestObject;
        }
    }
}
