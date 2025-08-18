// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEngine;

namespace Phanto
{
    /// <summary>
    /// Interface for damagable object
    /// </summary>
    public interface IDamageable
    {
        public delegate void DamageCallback(IDamageable damagableAffected, float hpAffected, bool targetDied);

        void Heal(float healing, DamageCallback callback = null);

        void TakeDamage(float damage, Vector3 position, Vector3 normal, GameObject damageSource = null, DamageCallback callback = null);
    }
}
