// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using PhantoUtils;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

namespace Phantom
{
    public class CrystalRangedTarget : RangedFurnitureTarget, ICrystalTarget
    {
        private const float CONTAMINATION_AMOUNT = 5.0f;

        [SerializeField] private Crystal cornerCrystal;
        [SerializeField] private GameObject portalQuad;

        private OVRScenePlane _scenePlane;

        private Transform _transform;

        private readonly List<Crystal> _crystals = new List<Crystal>();

        private float _contamination = 0.0f;

        private Coroutine _revealCoroutine = null;

        private void Awake()
        {
            _transform = transform;
        }

        private void OnDestroy()
        {
            DestroyCrystals();
        }

        public override void Initialize(OVRSemanticClassification classification, OVRSceneRoom room)
        {
            _semanticClassification = classification;
            _transform = transform;

            gameObject.SetSuffix($"{classification.Labels[0]}_{(ushort)gameObject.GetInstanceID():X4}");
            classification.GetComponentsInChildren(true, _colliders);
            Register(this, _colliders);

            if (!TryGetComponent(out _scenePlane))
            {
                Debug.LogError("No scene plane attached to crystal target", this);
            }

            SpawnCrystals(room);
            ShowCrystals(false);
            // these aren't valid targets until we're in the right gameplay phase.
            Activate(false);

            _planarTarget = classification.ContainsAny(PlanarTargets);
        }

        public override void Hide()
        {
            ShowCrystals(false);
            base.Hide();
        }

        public override void TakeDamage(float f)
        {
            // Take damage is called when the attack starts, not when goo ball hits.
        }

        public void Activate(bool active = true)
        {
            _active = active;
        }

        public void Reveal(bool visible = true)
        {
            // show the crystals on the frame.
            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
            }

            if (visible)
            {
                _revealCoroutine = StartCoroutine(RevealCrystals());
            }
            else
            {
                ShowCrystals(false);
            }
        }

        public void Attack(float r)
        {
            if (_contamination == 0)
            {
                return;
            }

            _contamination -= r;

            if (_contamination <= 0.0f)
            {
                ResetCrystal();
            }
        }

        private IEnumerator RevealCrystals()
        {
            Activate(false);
            // Reveal crystals.
            var count = _crystals.Count;

            _crystals.Shuffle();

            var wait = new WaitForSeconds(0.05f);

            for (int i = 0; i < count; i++)
            {
                _crystals[i].Show();
                if (Random.value > 0.33f)
                {
                    yield return wait;
                }
            }

            _revealCoroutine = null;
            _contamination = CONTAMINATION_AMOUNT;

            portalQuad.SetActive(true);
            Activate(true);
        }

        private void ResetCrystal()
        {
            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
            }

            _contamination = 0;

            // Remove crystals and reactivate the collider.
            ShowCrystals(false);
            Activate(false);
        }

        private void SpawnCrystals(OVRSceneRoom room)
        {
            Assert.IsNotNull(_transform);
            Assert.IsNotNull(_scenePlane);
            Assert.IsTrue(_scenePlane.Boundary.Count != 0);

            var forward = _transform.forward;
            var origin = _transform.position - (forward * 0.03f);

            var boundary = _scenePlane.Boundary;
            var count = boundary.Count;

            var offset = Vector3.zero;

            for (var i=0; i < count; i++)
            {
                // spawn and position corner crystals.
                // use boundary to spawn crystals.
                var pointA = boundary[i];

                // translate the corners
                var worldPointA = _transform.TransformPoint(pointA) + offset;

                var crystal = Instantiate(cornerCrystal, worldPointA, Quaternion.identity, _transform);

                var plane = new Plane(forward, worldPointA);
                var projectedOrigin = plane.ClosestPointOnPlane(origin);

                // Rotate corner crystals so pointing towards center of anchor.
                crystal.LookAtPoint(projectedOrigin, forward);
                crystal.RandomizeScale(0.9f, 1.1f);

                _crystals.Add(crystal);
            }
        }

        private Vector3 ClosestHitPoint(int hitCount, RaycastHit[] raycastHits, Vector3 originalPoint)
        {
            if (hitCount == 0)
            {
                return originalPoint;
            }

            if (hitCount == 1)
            {
                return raycastHits[0].point;
            }

            var result = originalPoint;
            var minDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var hitPoint = raycastHits[i].point;

                var distance = (originalPoint - hitPoint).sqrMagnitude;

                if (distance < minDistance)
                {
                    result = hitPoint;
                    minDistance = distance;
                }
            }

            return result;
        }

        private void ShowCrystals(bool visible = true)
        {
            var count = _crystals.Count;

            for (int i = 0; i < count; i++)
            {
                _crystals[i].Show(visible);
            }

            portalQuad.SetActive(visible);
        }

        private void DestroyCrystals()
        {
            foreach (var crystal in _crystals)
            {
                crystal.Destruct();
            }

            _crystals.Clear();
        }
    }
}
