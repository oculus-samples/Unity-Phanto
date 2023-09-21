// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;

namespace Phanto
{
    /// <summary>
    /// A projectile that shoots a simple projectile
    /// </summary>
    public class Projectile : MonoBehaviour, IDamageable
    {
        public float health = 10f;
        public float speed = 1f;

        private Rigidbody _rigidbody;

        [NonSerialized] public WeaponHitHandler hitHandler;

        private void Start()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            _rigidbody.velocity = speed * transform.forward;
        }

        protected void OnCollisionStay(Collision c)
        {
            TriggerImpact(c);
        }

        public void Heal(float healing, IDamageable.DamageCallback callback = null)
        {
        }

        /// <summary>
        /// Triggered when projectile hits something
        /// </summary>
        public void TakeDamage(float damage, Vector3 position, Vector3 normal,
            IDamageable.DamageCallback callback = null)
        {
            if (health <= 0f) return;

            health -= damage;
            if (health <= 0f) TriggerImpact();
        }

        public event Action Impacted;

        private void TriggerImpact(Collision c = null)
        {
            if (c != null)
                hitHandler?.ResolveHit(transform.position,
                    transform.forward,
                    c.GetContact(0).point,
                    c.GetContact(0).normal,
                    c.gameObject,
                    1f);
            hitHandler?.ResolveHits(transform.position,
                transform.forward,
                hitObj => hitObj == gameObject ||
                          (c != null && hitObj == c.gameObject));

            Impacted?.Invoke();
            PoolManagerSingleton.Instance.Discard(gameObject);
        }
    }
}
