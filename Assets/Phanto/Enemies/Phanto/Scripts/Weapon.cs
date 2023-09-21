// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Phanto
{
    /// <summary>
    ///     Weapon base class
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        [Tooltip("The layer to raycast for weapon raycasts.")]
        [SerializeField] private LayerMask raycastLayers = Physics.DefaultRaycastLayers;

        [Tooltip("The spread of the weapon when firing.")]
        [SerializeField] private Vector2 weaponSpread = new(0.01f, 0.05f);

        [Tooltip("The damage of the weapon when firing.")]
        [SerializeField] private Vector2 weaponDamage = new(8f, 14f);

        [Tooltip("The strength of the weapon when firing.")]
        [SerializeField] private Vector2 weaponKnockback = new(10f, 20f);

        [Tooltip("The rate at which this weapon fires once per second.")]
        [SerializeField] private float fireRate;

        [Tooltip("The number of projectiles to fire.")]
        [SerializeField] public uint WeaponNumProjectiles = 1;

        [Tooltip("The transform of the muzzle's hand.")]
        [SerializeField] protected Transform muzzleTransform;

        // Private
        private bool _isFiring;
        private bool _isFiringThisFrame;
        private float _shotTimer;

        [HideInInspector] public IDamageable.DamageCallback damageCallback = null;
        public LayerMask RaycastLayers => raycastLayers;

        public Vector2 WeaponSpread
        {
            get => weaponSpread;
            set => weaponSpread = value;
        }

        public Vector2 WeaponDamage => weaponDamage;
        public Vector2 WeaponKnockback => weaponKnockback;
        public float WeaponFireRate => fireRate;
        public Transform MuzzleTransform => muzzleTransform;

        public WeaponHitHandler HitHandler { get; set; }

        protected virtual void Start()
        {
            HitHandler = new WeaponHitHandler(this);
        }

        private void Update()
        {
            var isRapidFire = fireRate > 0.05f;
            _shotTimer -= Time.deltaTime;

            if (_isFiring && isRapidFire && _shotTimer <= 0f) _isFiringThisFrame = true;
        }

        private void FixedUpdate()
        {
            if (_isFiringThisFrame)
            {
                _isFiringThisFrame = false;
                _shotTimer = 1.0f / fireRate;
                Shoot();
            }
        }

        public event Action<Vector3, Vector3, uint> WeaponFired;
        public event Action StartedFiring;
        public event Action StoppedFiring;
        public event Action<Vector3, Vector3> HitsResolved;

        public void StartFiring()
        {
            _isFiring = true;
            _isFiringThisFrame = true;
            StartedFiring?.Invoke();
        }

        /// <summary>
        ///     Stop the firing of the weapon.
        /// </summary>
        public void StopFiring()
        {
            _isFiring = false;
            _isFiringThisFrame = false;
            StoppedFiring?.Invoke();
        }

        /// <summary>
        ///     Fires the weapon, including hit resolution.
        /// </summary>
        public virtual void Shoot()
        {
            if (HitHandler == null) return;

            var shotOrigin = muzzleTransform.position;
            Vector3 shotDir;

            for (uint u = 0; u < WeaponNumProjectiles; ++u)
            {
                shotDir = muzzleTransform.TransformDirection(WeaponUtils.RandomSpread(weaponSpread));
                Debug.DrawRay(shotOrigin, shotDir, Color.red, 1f);
                HitHandler.ResolveHits(shotOrigin, shotDir);
            }

            shotDir = muzzleTransform.TransformDirection(Vector3.forward);
            TriggerWeaponFired(shotOrigin, shotDir, WeaponNumProjectiles);
        }

        /// <summary>
        ///     Trigger an event when the weapon is fired.
        /// </summary>
        public void TriggerStartedFiring()
        {
            StartedFiring?.Invoke();
        }

        /// <summary>
        ///     Trigger an event when the weapon stopped firing.
        /// </summary>
        public void TriggerStoppedFiring()
        {
            StoppedFiring?.Invoke();
        }

        /// <summary>
        ///     Trigger an event when a projectile hits us.
        /// </summary>
        public void TriggerWeaponFired(Vector3 shotOrigin,
            Vector3 shotDir,
            uint numProjectiles)
        {
            WeaponFired?.Invoke(shotOrigin, shotDir, numProjectiles);
        }

        public void TriggerHitsResolved(Vector3 shotOrigin,
            Vector3 shotDir)
        {
            HitsResolved?.Invoke(shotOrigin, shotDir);
        }
    }

    /// <summary>
    ///     Weapon hit handler.
    /// </summary>
    public class WeaponHitHandler
    {
        public delegate void HitResolver(Vector3 shotOrigin,
            Vector3 shotDirection,
            Vector3 hitPosition,
            Vector3 hitNormal,
            GameObject hitObject,
            float hitStrength,
            bool applyForce = true,
            Func<IDamageable, bool> skipDamageable = null);

        public Weapon weapon;

        public WeaponHitHandler(Weapon weapon)
        {
            this.weapon = weapon;
        }

        public void ResolveHits(Vector3 shotOrigin,
            Vector3 shotDirection,
            Func<GameObject, bool> skipObject = null)
        {
            ResolveHits(shotOrigin,
                shotDirection,
                ResolveHit,
                skipObject);
        }

        public virtual void ResolveHits(Vector3 shotOrigin,
            Vector3 shotDirection,
            HitResolver hitResolver,
            Func<GameObject, bool> skipObject = null)
        {
            try
            {
                var hits = Physics.RaycastAll(shotOrigin,
                    shotDirection,
                    Mathf.Infinity,
                    weapon.RaycastLayers,
                    QueryTriggerInteraction.Ignore);

                if (hits.Length <= 0) return;

                var closestHit = hits[0];
                for (var i = 1; i < hits.Length; ++i)
                {
                    if (skipObject != null && skipObject(hits[i].rigidbody == null
                            ? hits[i].transform.gameObject
                            : hits[i].rigidbody.gameObject))
                        continue;

                    if (hits[i].distance < closestHit.distance) closestHit = hits[i];
                }

                hitResolver(shotOrigin,
                    shotDirection,
                    closestHit.point,
                    closestHit.normal,
                    closestHit.rigidbody == null ? closestHit.transform.gameObject : closestHit.rigidbody.gameObject,
                    Random.Range(0f, 1f));
            }
            finally
            {
                weapon.TriggerHitsResolved(shotOrigin, shotDirection);
            }
        }

        public virtual void ResolveHit(Vector3 shotOrigin,
            Vector3 shotDirection,
            Vector3 hitPosition,
            Vector3 hitNormal,
            GameObject hitObject,
            float hitStrength,
            bool applyForce = true,
            Func<IDamageable, bool> skipDamageable = null)
        {
            hitObject.TryGetComponent(out Rigidbody hitBody);
            if (applyForce && hitBody)
                hitBody.AddForceAtPosition(
                    Mathf.Lerp(weapon.WeaponKnockback.x, weapon.WeaponKnockback.y, hitStrength) * shotDirection,
                    hitPosition,
                    ForceMode.Impulse);

            foreach (var damageable in hitObject.GetComponents<IDamageable>())
            {
                if (skipDamageable != null && skipDamageable(damageable)) continue;

                var damage = Mathf.Lerp(weapon.WeaponDamage.x, weapon.WeaponDamage.y, hitStrength);
                damageable.TakeDamage(damage, hitPosition, hitNormal, weapon.damageCallback);
            }
        }
    }
}
