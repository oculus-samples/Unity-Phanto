// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Phantom.Environment.Scripts;
using UnityEngine;
using UnityEngine.Assertions;

namespace Phanto
{
    /// <summary>
    ///     This class implements a "goo-ball" that bounces around a virtual environment and spreads goo.
    /// </summary>
    public class PhantoGooBall : MonoBehaviour
    {
        [SerializeField] private float maxbounces = 3;
        [SerializeField] private new Rigidbody rigidbody;
        [SerializeField] private float maxlifeTime = 10;

        private int _numBounces;
        private float _startTime;
        private float _lifeTime;

        private readonly List<ContactPoint> _contactPoints = new(256);

        private void FixedUpdate()
        {
            _lifeTime += Time.deltaTime;

            if (Time.time - _startTime < 2.0f) return;

            if (rigidbody.velocity.sqrMagnitude <= Vector3.kEpsilon * Vector3.kEpsilon || _lifeTime > maxlifeTime)
            {
                PoolManagerSingleton.Instance.Discard(gameObject);
            }
        }

        private void OnEnable()
        {
            _startTime = Time.time;
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

            Phanto.Instance.StartGoo(offsetPoint, Quaternion.LookRotation(contact.normal));
            _numBounces++;
            if (_numBounces >= maxbounces) PoolManagerSingleton.Instance.Discard(gameObject);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (rigidbody == null) rigidbody = GetComponent<Rigidbody>();
        }
#endif
    }
}
