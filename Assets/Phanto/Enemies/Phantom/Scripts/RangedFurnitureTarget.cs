// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using Phanto.Enemies.DebugScripts;
using PhantoUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using Utilities.XR;
using Classification = OVRSceneManager.Classification;

namespace Phantom
{
    /// <summary>
    ///     This is for furniture that isn't walkable (lamp, plant, etc.)
    ///     Phantoms will approach these targets and perform a ranged attack to goo them.
    /// </summary>
    public class RangedFurnitureTarget : PhantomTarget
    {
        protected static readonly string[] PlanarTargets = new[] { OVRSceneManager.Classification.DoorFrame, OVRSceneManager.Classification.WindowFrame, OVRSceneManager.Classification.WallArt };

        protected readonly List<Collider> _colliders = new();
        protected bool _active = true;

        protected OVRSemanticClassification _semanticClassification;
        protected readonly List<NavMeshTriangle> _triangles = new List<NavMeshTriangle>();
        private RaycastHit[] sphereCastHits = new RaycastHit[256];

        public string Classification => _semanticClassification.Labels[0];

        public override bool Flee => false;

        protected bool _planarTarget = false;

        public override Vector3 Position
        {
            get => transform.position;
            set => transform.position = value;
        }

        public override bool Valid => _active;

        protected override void OnEnable()
        {
            _active = true;
            Register(this, _colliders);
            DebugDrawManager.DebugDrawEvent += DebugDraw;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _active = false;
            Unregister(this, _colliders);
            DebugDrawManager.DebugDrawEvent -= DebugDraw;
        }

        public override void Initialize(OVRSemanticClassification classification, OVRSceneRoom _)
        {
            _semanticClassification = classification;

            gameObject.SetSuffix($"{classification.Labels[0]}_{(ushort)gameObject.GetInstanceID():X4}");
            classification.GetComponentsInChildren(true, _colliders);
            Register(this, _colliders);

            _planarTarget = classification.ContainsAny(PlanarTargets);
        }

        private Vector3 RandomPointOnCollider()
        {
            Assert.IsFalse(_colliders.Count == 0);
            var collider = _colliders.RandomElement();
            return collider.ClosestPoint(collider.bounds.RandomPoint());
        }

        public override Vector3 GetAttackPoint()
        {
            return RandomPointOnCollider();
        }

        public override void TakeDamage(float f)
        {
            // disable this target for a few (~10?) seconds
            StartCoroutine(SleepForSeconds(Random.Range(8.0f, 12.0f)));
        }

        protected IEnumerator SleepForSeconds(float seconds)
        {
            _active = false;
            yield return new WaitForSeconds(seconds);
            _active = true;
        }

        public override Vector3 GetDestination(Vector3 origin, float min = 0.0f, float max = 0.0f)
        {
            Vector3 launchPoint = default;
            // get a point somewhere on the target object.
            var targetPoint = RandomPointOnCollider();

            // find the triangles on the navmesh that are on a circle around the selected point.
            var distance = Random.Range(0.5f, 1.0f);
            int count = NavMeshBookKeeper.TrianglesOnCircle(targetPoint, distance, true, _triangles);

            // constrain attack points to the front ~140 of planar targets.
            if (count > 0 && _planarTarget)
            {
                var position = Position;
                var forwardNormal = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

                _triangles.RemoveAll((tri) =>
                {
                    var vector = Vector3.ProjectOnPlane(  position - tri.center, Vector3.up).normalized;

                    var dot = Vector3.Dot(forwardNormal, vector);

                    return dot > 0.33f;
                });

                count = _triangles.Count;
            }

            for (var i = 0; i < 10; i++)
            {
                if (count != 0)
                {
                    var triangle = _triangles.RandomElement();
                    var randomPoint = triangle.GetRandomPoint();

                    // the random point on the triangle could be further away than attack range.
                    // so we find a point on the line from target to launch point to fire from.
                    var plane = new Plane(Vector3.up, randomPoint);
                    targetPoint = plane.ClosestPointOnPlane(targetPoint);

                    var ray = new Ray(targetPoint,  randomPoint - targetPoint);
                    launchPoint = ray.GetPoint(distance);
                }
                else
                {
                    // if there are no open triangles on that circle, fall back to the closest available.
                    var triangle = NavMeshBookKeeper.ClosestTriangleOnCircle(targetPoint, Random.Range(0.5f, 1.0f), true);
                    launchPoint = triangle.GetRandomPoint();
                }

                if (NavMesh.SamplePosition(launchPoint, out var navMeshHit, NavMeshConstants.OneFoot, NavMesh.AllAreas)) {
                    launchPoint = navMeshHit.position;
                    break;
                }
            }

            return launchPoint;
        }

        public override void Show(bool visible = true)
        {
        }

        private void DebugDraw()
        {
            foreach (var collider in _colliders)
            {
                if (collider is BoxCollider boxCollider)
                {
                    var boxTransform = boxCollider.transform;

                    var scale = boxTransform.lossyScale;
                    var size = boxCollider.size;
                    size.Set(size.x * scale.x, size.y * scale.y, size.z * scale.z);

                    XRGizmos.DrawWireCube(boxTransform.TransformPoint(boxCollider.center), boxTransform.rotation, size,
                        _active ? MSPalette.Orange : Color.black);

                }
                else
                {
                    var bounds = collider.bounds;
                    XRGizmos.DrawWireCube(bounds.center, Quaternion.identity, bounds.size,
                        _active ? MSPalette.Orange : Color.black);
                }
            }
        }
    }
}
