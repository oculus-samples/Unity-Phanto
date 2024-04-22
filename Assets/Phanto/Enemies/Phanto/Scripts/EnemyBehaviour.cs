// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using UnityEngine;
using Logger = PhantoUtils.Logger;

namespace Phanto
{
    /// <summary>
    ///     Abstract base class for the behaviour of an Enemy
    /// </summary>
    public abstract class EnemyBehaviour<B> : MonoBehaviour,
        IDamageable,
        EnemyProximitySensor.IProximityTrigger where B : EnemyBehaviour<B>
    {
        private static bool exitingState;
        protected IEnemyState<B> curState;

        protected internal Enemy e;

        protected abstract uint InitialState { get; }

        protected virtual void Awake()
        {
            e = GetComponent<Enemy>();

            curState = GetState(InitialState);
            curState.EnterState((B)this, curState);
        }

        private void FixedUpdate()
        {
            UpdateBehaviour();
        }

        protected void OnCollisionStay(Collision c)
        {
            curState.OnCollisionStay((B)this, c);
        }

        public abstract void Heal(float healing, IDamageable.DamageCallback callback = null);

        public abstract void TakeDamage(float damage, Vector3 position, Vector3 normal, GameObject damageSource = null,
            IDamageable.DamageCallback callback = null);

        public void OnProximityStay(Collider c)
        {
            curState.OnProximityStay((B)this, c);
        }

        public abstract uint GetNumStates();

        public abstract IEnemyState<B> GetState(uint stateID);

        public IEnemyState<B> GetCurrentState()
        {
            return curState;
        }

        public void SwitchState(uint nextStateID)
        {
            if (exitingState)
            {
                Logger.Log(Logger.Type.Error, Logger.Severity.Verbose,
                    curState.GetType().Name + " attempted to SwitchState() within ExitState()!", this);
                return;
            }

            var nextState = GetState(nextStateID);
            var lastState = curState;

            if (nextState == lastState)
            {
#if DEBUG && VERBOSE_DEBUG
                Debug.LogWarning($"Self state transition {nextState.GetType().Name}=>{lastState.GetType().Name}");
#endif
                return;
            }

            exitingState = true;
            curState?.ExitState((B)this, nextState);
            exitingState = false;
            curState = nextState;
            curState.EnterState((B)this, lastState);
        }

        protected virtual void UpdateBehaviour()
        {
            curState.UpdateState((B)this);
        }
    }
}
