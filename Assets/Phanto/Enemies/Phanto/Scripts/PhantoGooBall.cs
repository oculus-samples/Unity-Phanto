// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using Phantom;
using Phantom.Environment.Scripts;
using PhantoUtils;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace Phanto
{
    /// <summary>
    ///     This class implements a "goo-ball" that bounces around a virtual environment and spreads goo.
    /// </summary>
    public class PhantoGooBall : MonoBehaviour
    {
        private static readonly Dictionary<Object, PhantoGooBall> _gooBalls = new Dictionary<Object, PhantoGooBall>();

        [SerializeField] private float maxbounces = 3;
        [SerializeField] private new Rigidbody rigidbody;
        [SerializeField] private float maxlifeTime = 10;

        private int _numBounces;
        private float _startTime;
        private float _lifeTime;

        private Collider _collider;

        private readonly List<ContactPoint> _contactPoints = new(256);

        private GameplaySettings.WinCondition _winCondition = GameplaySettings.WinCondition.DefeatPhanto;

        private PhantomBehaviour _launcher;

        private PoolManagerSingleton _poolManager;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            Assert.IsNotNull(_collider);

            if (rigidbody == null)
            {
                rigidbody = GetComponent<Rigidbody>();
            }

            Assert.IsNotNull(rigidbody);

            _gooBalls[_collider] = this;
            _gooBalls[transform] = this;
            _gooBalls[gameObject] = this;
        }

        private void Start()
        {
            _poolManager = PoolManagerSingleton.Instance;

            Assert.IsNotNull(_poolManager);
        }

        private void OnEnable()
        {
            _startTime = Time.time;
            _winCondition = GameplaySettings.WinCondition.DefeatPhanto;
        }

        private void OnDisable()
        {
            _launcher = null;
            _numBounces = 0;
        }

        private void OnDestroy()
        {
            _gooBalls.Remove(_collider);
            _gooBalls.Remove(transform);
            _gooBalls.Remove(gameObject);
        }

        private void FixedUpdate()
        {
            _lifeTime += Time.deltaTime;

            if (Time.time - _startTime < 2.0f) return;

            if (_lifeTime > maxlifeTime || rigidbody.velocity.sqrMagnitude <= Vector3.kEpsilon * Vector3.kEpsilon)
            {
                PoolManagerSingleton.Instance.Discard(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!PhantoSceneMesh.IsSceneMesh(collision.gameObject))
            {
                return;
            }

            var count = collision.GetContacts(_contactPoints);
            Assert.IsTrue(count > 0);

            var contact = _contactPoints[0];

            // to avoid z-fighting with the hit surface, move the goo a tiny bit above surface.
            var offsetPoint = contact.point + contact.normal * 0.002f;

            var gooObject = _poolManager.StartGoo(offsetPoint, Quaternion.LookRotation(contact.normal));

            switch (_winCondition)
            {
                case GameplaySettings.WinCondition.DefeatPhanto:
                    break;
                case GameplaySettings.WinCondition.DefeatAllPhantoms:
                    // Non-permanent impact VFX/SFX
                    if (gooObject.TryGetComponent<PhantoGoo>(out var goo))
                    {
                        goo.Extinguish();
                        PoolManagerSingleton.Instance.Discard(gameObject);
                        return;
                    }
                    break;
            }

            _numBounces++;
            if (_numBounces >= maxbounces) PoolManagerSingleton.Instance.Discard(gameObject);
        }

        public void NotifyLauncher()
        {
            // if projectile hit a target notify the mob that launched the projectile
            // so they know they're in a good position.
            if (_launcher != null)
            {
                _launcher.SuccessfulLaunch();
            }
        }

        public void LaunchProjectile(Vector3 launchPoint, Vector3 launchVector,
            GameplaySettings.WinCondition winCondition, PhantomBehaviour launcher)
        {
            _launcher = launcher;
            _winCondition = winCondition;

            rigidbody.LaunchProjectile(launchPoint, launchVector);
        }

        public static bool TryGetGooBall(Object other, out PhantoGooBall result)
        {
            return _gooBalls.TryGetValue(other, out result);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (rigidbody == null) rigidbody = GetComponent<Rigidbody>();
        }
#endif
    }
}
