// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Phanto
{
    /// <summary>
    ///     Interface for the enemy game states.
    /// </summary>
    public interface IEnemyState<B> where B : EnemyBehaviour<B>
    {
        uint GetStateID();
        void EnterState(B b, IEnemyState<B> lastState);
        void OnCollisionStay(B b, Collision c);
        void OnProximityStay(B b, Collider c);
        void UpdateState(B b);
        void ExitState(B b, IEnemyState<B> nextState);
    }
}
