// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using Phanto;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace Phantom.Environment.Scripts
{
    /// <summary>
    /// The PhantoSceneMesh class stores information about a scene
    /// </summary>
    public class PhantoSceneMesh : MonoBehaviour
    {
        private const float SPATIAL_HASH_CELL_SIZE = 0.05f;

        private static readonly Dictionary<Object, PhantoSceneMesh> meshCollection = new();

        private readonly List<ParticleCollisionEvent> _pces = new(1024);

        [SerializeField] private new Collider collider;

        private OVRSceneRoom _room;

        private Transform _roomTransform;

        private readonly SpatialHash<CrystalRangedTarget> _spatialHash = new(SPATIAL_HASH_CELL_SIZE);
        private readonly RaycastHit[] _crystalHits = new RaycastHit[256];

        private bool _ready = false;

        private void Awake()
        {
            meshCollection[collider] = this;
            meshCollection[transform] = this;
            meshCollection[gameObject] = this;
        }

        private IEnumerator Start()
        {
            do
            {
                _room = GetComponentInParent<OVRSceneRoom>(true);
                yield return null;
            } while (_room == null);

            Assert.IsNotNull(_room);
            _roomTransform = _room.transform;

            _ready = true;
        }

        private void OnDestroy()
        {
            if (collider != null) meshCollection.Remove(collider);

            meshCollection.Remove(transform);
            meshCollection.Remove(gameObject);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (collider == null) collider = GetComponent<Collider>();
        }
#endif

        public static bool IsSceneMesh(Object other)
        {
            return meshCollection.ContainsKey(other);
        }

        public static bool TryGetSceneMesh(Object other, out PhantoSceneMesh sceneMesh)
        {
            return meshCollection.TryGetValue(other, out sceneMesh);
        }

        private void OnCollisionEnter(Collision other)
        {
            // make sure 'other' is a goo ball.
            if (!_ready || !PhantoGooBall.TryGetGooBall(other.collider, out var gooBall))
            {
                return;
            }

            // goo ball hit the scene mesh. check to see if there is a
            // collider just behind the scene mesh (i.e. window, door, art plane)

            var contact = other.GetContact(0);
            CrystalTargetProbe(contact.point, contact.normal, (target) =>
            {
                gooBall.NotifyLauncher();
                target.Attack(1);
            });
        }

        private void OnParticleCollision(GameObject other)
        {
            if (_ready || !PolterblastTrigger.TryGetPolterblaster(other, out var trigger))
            {
                return;
            }

            // player sprayed the scene mesh with the blaster.
            // is there a window/door/art slightly behind the mesh?

            var ps = trigger.EctoParticleSystem;
            var count = ps.GetCollisionEvents(gameObject, _pces);

            var avgPCIntersection = Vector3.zero;
            var avgPCNormal = Vector3.zero;

            for (var i = 0; i < count; i++)
            {
                var pce = _pces[i];

                avgPCNormal += pce.normal;
                avgPCIntersection += pce.intersection;
            }

            avgPCIntersection /= count;
            avgPCNormal /= count;

            // convert the point/normal into room-relative coordinates in case room moves in the future;
            var roomPoint = _roomTransform.InverseTransformPoint(avgPCIntersection);
            var roomNormal = _roomTransform.InverseTransformDirection(avgPCNormal);

            // FIXME: what do we do when the player sprays a window door? Heal damage? (Makes round too easy?)
            // CrystalTargetProbe(roomPoint, roomNormal, (crystalTarget) =>
            // {
            //     crystalTarget.Heal(1);
            // });
        }

        /// <summary>
        /// Invokes the callback for each PhantomCrystalTarget discovered behind the scene mesh.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="normal"></param>
        /// <param name="callback"></param>
        private void CrystalTargetProbe(Vector3 point, Vector3 normal, Action<CrystalRangedTarget> callback)
        {
            // check to see if the spatial hash has info about this point
            // is there a window/door/wall art behind the scene mesh at this point?
            if (!_spatialHash.TryGetCell(point, out var contents))
            {
                int hitCount = 0;

                // cast a ray from intersection looking for window/door/wall art
                var ray = new Ray(point, -normal);

                hitCount = Physics.SphereCastNonAlloc(ray, NavMeshConstants.TennisBall, _crystalHits, NavMeshConstants.OneFoot);

                // no door/window/art behind scene mesh at this point.
                if (hitCount == 0)
                {
                    _spatialHash.Add(point, null);
                    return;
                }

                for (var i = 0; i < hitCount; i++)
                {
                    // look up if the collider is associated with a crystal target.
                    // if it is register it in the spatial hash and pass hit.

                    var hitCollider = _crystalHits[i].collider;

                    if (PhantomTarget.TryGetTarget(hitCollider, out var target) && target is CrystalRangedTarget crystalTarget)
                    {
                        _spatialHash.Add(point, crystalTarget);
                        // pass hit to target.
                        callback?.Invoke(crystalTarget);
                    }
                }

                return;
            }

            foreach (var target in contents)
            {
                if (target == null)
                {
                    continue;
                }

                // pass hit to target.
                callback?.Invoke(target);
            }
        }
    }
}
