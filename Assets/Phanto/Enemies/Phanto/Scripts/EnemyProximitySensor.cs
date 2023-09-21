// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Phanto
{
    /// <summary>
    ///     Proximity sensor for enemies. This will use the IProximityTrigger in the game
    /// </summary>
    public class EnemyProximitySensor : MonoBehaviour
    {
        [SerializeField] private SphereCollider sphereCollider;

        private IProximityTrigger[] _proximityTriggers;
        public float Radius => sphereCollider == null ? 0f : sphereCollider.radius;

        private void Awake()
        {
            _proximityTriggers = GetComponents<IProximityTrigger>();
        }

        private void OnTriggerStay(Collider c)
        {
            foreach (var trigger in _proximityTriggers) trigger.OnProximityStay(c);
        }

        public interface IProximityTrigger
        {
            void OnProximityStay(Collider c);
        }
    }
}
