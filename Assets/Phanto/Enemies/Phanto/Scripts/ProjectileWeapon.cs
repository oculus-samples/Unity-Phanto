// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;
using Logger = PhantoUtils.Logger;

namespace Phanto
{
    /// <summary>
    ///     Weapon class for projectiles.
    /// </summary>
    public class ProjectileWeapon : Weapon
    {
        public enum DamageFalloff
        {
            NONE,
            LINEAR,
            QUADRATIC
        }

        public Projectile projectilePrefab;
        public float damageRadius = 1f;
        public Vector2 splashDamage = new(10f, 60f);
        public DamageFalloff damageFalloff = DamageFalloff.QUADRATIC;

        protected override void Start()
        {
            HitHandler = new ProjectileWeaponHitHandler(this);
        }

        /// <summary>
        ///     Fires a projectile.
        /// </summary>
        public override void Shoot()
        {
            if (HitHandler == null) return;

            var shotOrigin = muzzleTransform.position;
            var shotDir = muzzleTransform.TransformDirection(Vector3.forward);

            for (uint u = 0; u < WeaponNumProjectiles; ++u)
            {
                shotDir = muzzleTransform.TransformDirection(WeaponUtils.RandomSpread(WeaponSpread));

                var q = new Quaternion();
                q.SetLookRotation(shotDir);
                var projectile = PoolManagerSingleton.Instance.Create(projectilePrefab, shotOrigin, q);
                if (projectile != null) projectile.hitHandler = HitHandler;
            }

            TriggerWeaponFired(shotOrigin, shotDir, WeaponNumProjectiles);
        }
    }

    /// <summary>
    ///     ProjectileWeapon hit handler.
    /// </summary>
    public class ProjectileWeaponHitHandler : WeaponHitHandler
    {
        private readonly ProjectileWeapon _projWeapon;

        public ProjectileWeaponHitHandler(ProjectileWeapon w) : base(w)
        {
            _projWeapon = w;
        }

        public override void ResolveHits(Vector3 shotOrigin,
            Vector3 shotDirection,
            HitResolver hitResolver,
            Func<GameObject, bool> skipObject = null)
        {
            var numHits = Physics.OverlapSphereNonAlloc(shotOrigin,
                _projWeapon.damageRadius,
                PhysicsUtils.colliderResults,
                _projWeapon.RaycastLayers);
            numHits = PhysicsUtils.DeduplicateHits(shotOrigin, PhysicsUtils.colliderResults, numHits);

            for (var i = numHits - 1;
                 i >= 0;
                 --i)
            {
                var hit = PhysicsUtils.colliderResults[i];
                var hitObj = hit.attachedRigidbody == null ? hit.gameObject : hit.attachedRigidbody.gameObject;
                if (skipObject != null && skipObject(hitObj)) continue;

                shotDirection = hit.ClosestPoint(shotOrigin) - shotOrigin;
                var strength = 1f;
                switch (_projWeapon.damageFalloff)
                {
                    case ProjectileWeapon.DamageFalloff.QUADRATIC:
                        strength = (_projWeapon.damageRadius * _projWeapon.damageRadius - shotDirection.sqrMagnitude) /
                                   _projWeapon.damageRadius;
                        break;
                    case ProjectileWeapon.DamageFalloff.LINEAR:
                        strength = (_projWeapon.damageRadius - shotDirection.magnitude) / _projWeapon.damageRadius;
                        break;
                    case ProjectileWeapon.DamageFalloff.NONE:
                        break;
                    default:
                        strength = 1f;
                        break;
                }

                shotDirection.Normalize();

                var rayHit = hit.Raycast(new Ray(shotOrigin, shotDirection), out var hitInfo,
                    _projWeapon.damageRadius + 1000f);

                if (!rayHit)
                    Logger.Log(Logger.Type.Error, Logger.Severity.Verbose,
                        "Projectile ray missed, could not grab normal!", _projWeapon);

                hitResolver(shotOrigin,
                    shotDirection,
                    hitInfo.point,
                    hitInfo.normal,
                    hitObj,
                    strength);
            }
        }

        public override void ResolveHit(Vector3 shotOrigin,
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

                var damage = Mathf.Lerp(_projWeapon.splashDamage.x, _projWeapon.splashDamage.y, hitStrength);
                if (hitStrength >= 1f) damage += weapon.WeaponDamage.y;
                damageable.TakeDamage(damage, hitPosition, hitNormal, weapon.damageCallback);
            }
        }
    }
}
