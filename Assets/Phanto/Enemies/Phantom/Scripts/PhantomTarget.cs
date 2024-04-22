// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Phantom
{
    /// <summary>
    ///     Base class for all phantom targets in the game.
    /// </summary>
    public abstract class PhantomTarget : MonoBehaviour
    {
        private static readonly Dictionary<Collider, PhantomTarget> TargetCollection = new();
        public static IReadOnlyCollection<PhantomTarget> AvailableTargets => TargetCollection.Values;

        public abstract Vector3 Position { get; set; }
        public abstract bool Valid { get; }
        public abstract bool Flee { get; }

        public abstract void TakeDamage(float f);

        /// <summary>
        /// Returns a world space position the agent should path to in order to reach or flee a target.
        /// </summary>
        /// <param name="point">point is where the agent is moving from</param>
        /// <param name="min">closest agent wants to get</param>
        /// <param name="max">furthest agent wants to get</param>
        /// <returns></returns>
        public abstract Vector3 GetDestination(Vector3 point, float min, float max);

        public abstract void Show(bool visible = true);

        public abstract Vector3 GetAttackPoint();

        /// <summary>
        ///     When this target is returned to the pool anyone that had it as a current target should forget it.
        /// </summary>
        public event Action<PhantomTarget> Forget;

        protected virtual void OnEnable()
        {
        }

        protected virtual void OnDisable()
        {
        }

        public virtual void Hide()
        {
            Forget?.Invoke(this);
            Forget = null;
            Show(false);
        }

        public void Dispatch(Vector3 point)
        {
            Assert.AreNotEqual(point, Vector3.zero);
            transform.position = point;
            Show();
        }

        public void Destruct()
        {
            Destroy(gameObject);
        }

        public abstract void Initialize(OVRSemanticClassification classification, OVRSceneRoom room);

        public static bool TryGetTarget(Collider collider, out PhantomTarget target)
        {
            return TargetCollection.TryGetValue(collider, out target);
        }

        protected static void Register(PhantomTarget target, IList<Collider> colliders)
        {
            if (colliders == null) return;

            foreach (var collider in colliders) TargetCollection[collider] = target;
        }

        protected static void Unregister(PhantomTarget target, IList<Collider> colliders)
        {
            if (colliders == null) return;

            foreach (var collider in colliders) TargetCollection.Remove(collider);
        }

        protected static void Register(PhantomTarget target, Collider collider)
        {
            if (collider == null) return;

            TargetCollection[collider] = target;
        }

        protected static void Unregister(PhantomTarget target, Collider collider)
        {
            if (collider == null) return;

            TargetCollection.Remove(collider);
        }
    }
}
