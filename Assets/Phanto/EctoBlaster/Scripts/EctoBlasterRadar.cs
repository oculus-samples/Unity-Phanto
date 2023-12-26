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
        private Transform blasterBaseTransform;

        [Tooltip("The rotation speed to rotate to the desired target")] [SerializeField]
        private float rotationSpeed = 0.5f;

        [Tooltip("Time to perform scan loop for targets")] [SerializeField]
        private float scanTime = 1.0f;

        private PolterblastTrigger _blasterTrigger;

        private IReadOnlyCollection<PhantomController> _targets;

        public float BlastRadius { get; set; } = 0.5f;

        private IEnumerator Start()
        {
            _blasterTrigger = GetComponentInChildren<PolterblastTrigger>(true);
            Assert.IsNotNull(_blasterTrigger);
            Assert.IsNotNull(blasterBaseTransform);

            yield return null;

            while (true)
            {
                //Locate and update targets
                _targets = PhantomManager.Instance.ActivePhantoms;
                if (_targets.Count == 0) yield return new WaitForSeconds(scanTime);

                //Find best target and check range
                var target = FindTarget(_targets);
                var targetFound = target != null &&
                                  Vector3.Distance(blasterBaseTransform.position, target.HeadPosition) < BlastRadius;

                //Ecto blaster if a target was found and start rotation
                _blasterTrigger.AutomaticEnabled = targetFound;
                if (targetFound)
                    yield return StartCoroutine(RotateToDirection(blasterBaseTransform, target.HeadPosition,
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
            return FindClosestObject(targets, blasterBaseTransform.position);
        }

        /// <summary>
        ///     Rotates the transform to the desired target
        /// </summary>
        /// <param name="transformToRotate">The transform to change</param>
        /// <param name="positionToLook">Target position to look at</param>
        /// <param name="timeToRotate">Desired movement time</param>
        /// <returns></returns>
        private static IEnumerator RotateToDirection(Transform transformToRotate, Vector3 positionToLook,
            float timeToRotate)
        {
            var startRotation = transformToRotate.rotation;
            var direction = positionToLook - transformToRotate.position;
            var finalRotation = Quaternion.LookRotation(direction);
            var time = 0f;
            while (time <= 1f)
            {
                time += Time.deltaTime / timeToRotate;
                transformToRotate.rotation = Quaternion.Lerp(startRotation, finalRotation, time);
                yield return null;
            }

            transformToRotate.rotation = finalRotation;
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
