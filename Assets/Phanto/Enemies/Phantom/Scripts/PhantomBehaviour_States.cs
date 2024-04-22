// Copyright (c) Meta Platforms, Inc. and affiliates.

using Phanto;
using Phanto.Audio.Scripts;
using PhantoUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using Utilities.XR;

namespace Phantom
{
    /// <summary>
    ///     Represents a state in the phantom behaviour.
    /// </summary>
    public partial class PhantomBehaviour
    {
        private abstract class BasePhantomState : IEnemyState<PhantomBehaviour>
        {
            protected static readonly Vector3 CubeSize = new(0.05f, 0.1f, 0.05f);

            public abstract uint GetStateID();
            public abstract void EnterState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> lastState);
            public abstract void OnCollisionStay(PhantomBehaviour b, Collision c);
            public abstract void OnProximityStay(PhantomBehaviour b, Collider c);
            public abstract void UpdateState(PhantomBehaviour b);
            public abstract void ExitState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> nextState);

            public virtual void DebugDraw(PhantomBehaviour b)
            {
            }

            protected void DrawCubeOverHead(PhantomBehaviour b, Color color)
            {
                XRGizmos.DrawCube(b.HeadPosition, Quaternion.identity, CubeSize, color);
            }
        }

        private class Roam : BasePhantomState
        {
            public override uint GetStateID()
            {
                return (uint)StateID.Roam;
            }

            public override void EnterState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> lastState)
            {
                b.SetRandomDestination();
                b.ShowThought(Thought.Question);
                b.PlayEmote(Thought.Alert);
            }

            public override void OnCollisionStay(PhantomBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantomBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantomBehaviour b)
            {
                if (!b._onGround) return;

                b.CheckForTargets();
            }

            public override void ExitState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantomBehaviour b)
            {
                DrawCubeOverHead(b, Color.grey);
            }
        }

        private class Chase : BasePhantomState
        {
            private PhantomTarget _target;

            public override uint GetStateID()
            {
                return (uint)StateID.Chase;
            }

            public override void EnterState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> lastState)
            {
                _target = b.CurrentTarget;

                if (_target == null)
                {
                    Debug.LogWarning("Started chasing a null target");
                    return;
                }

                var destination =
                    _target.GetDestination(b.Position, b.MeleeRange * 0.5f, Mathf.Max(0, b.SpitRange - 0.03f));
                b.SetDestination(_target, destination);

                b.ShowTargetThought(_target);
                b.PlayEmote(Thought.Exclamation);
            }

            public override void OnCollisionStay(PhantomBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantomBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantomBehaviour b)
            {
                if (!b._onGround) return;

                b.SetRoamIfInvalidTarget();
            }

            public override void ExitState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantomBehaviour b)
            {
                DrawCubeOverHead(b, Color.green);
                if (_target != null)
                    XRGizmos.DrawPointer(b.HeadPosition, _target.Position - b.HeadPosition, Color.green, 0.3f);
            }
        }

        private class CrystalRoam : BasePhantomState
        {
            public override uint GetStateID()
            {
                return (uint)StateID.CrystalRoam;
            }

            public override void EnterState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> lastState)
            {
                b.ShowThought(Thought.Question);
                b.PlayEmote(Thought.Alert);
            }

            public override void OnCollisionStay(PhantomBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantomBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantomBehaviour b)
            {
                if (!b._onGround) return;

                b.CheckForTargets();
            }

            public override void ExitState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantomBehaviour b)
            {
                DrawCubeOverHead(b, MSPalette.Salmon);
            }
        }

        private class CrystalChase : BasePhantomState
        {
            private PhantomTarget _target;

            public override uint GetStateID()
            {
                return (uint)StateID.CrystalChase;
            }

            public override void EnterState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> lastState)
            {
                _target = b.CurrentTarget;
                var destination =
                    _target.GetDestination(b.Position, b.MeleeRange * 0.5f, Mathf.Max(0, b.SpitRange - 0.03f));

                b.SetDestination(_target, destination);

                b.ShowTargetThought(_target);
                b.PlayEmote(Thought.Exclamation);
            }

            public override void OnCollisionStay(PhantomBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantomBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantomBehaviour b)
            {
                if (!b._onGround) return;

                b.SetRoamIfInvalidTarget();
            }

            public override void ExitState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantomBehaviour b)
            {
                DrawCubeOverHead(b, Color.green);
                if (_target != null)
                    XRGizmos.DrawPointer(b.HeadPosition, _target.Position - b.HeadPosition, Color.green, 0.3f);
            }
        }

        private class Flee : BasePhantomState
        {
            private PhantomFleeTarget _target;

            public override uint GetStateID()
            {
                return (uint)StateID.Flee;
            }

            public override void EnterState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> lastState)
            {
                b.CancelMovement();
                _target = b.CurrentTarget as PhantomFleeTarget;

                Assert.IsNotNull(_target);

                var startPosition = _target.Position;
                var fleeRay = new Ray(startPosition, Vector3.forward);

                var originalFleeVector = _target.FleeVector;
                // Zero flee vector indicates a point you should run away from (turret)
                if (originalFleeVector == Vector3.zero)
                {
                    originalFleeVector = Vector3.ProjectOnPlane(b.Position - _target.Position, Vector3.up).normalized;
                }

                var fleeVector = originalFleeVector;
                var maxDistance = 0.0f;

                Vector3? finalDestination = null;

                // generate some random rays that will move agent away from target
                // pick the longest one.
                for (var i = 0; i < 8; i++)
                {
                    fleeRay.direction = fleeVector;

                    var endPoint = fleeRay.GetPoint(NavMeshConstants.OneFoot * 2.0f);

                    // navmesh raycast away from flee target.
                    NavMesh.Raycast(startPosition, endPoint, out var navMeshHit, NavMesh.AllAreas);
                    var destination = navMeshHit.position;

                    var distance = Vector3.Distance(startPosition, destination);

                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        finalDestination = destination;
                    }
                    else if (Mathf.Approximately(maxDistance, distance))
                    {
                        // the distances are equal toss a coin to pick one.
                        finalDestination = Random.value > 0.5f
                            ? destination
                            : finalDestination.GetValueOrDefault(destination);
                    }

                    // Randomly rotate the flee vector to test alternate paths to run away.
                    fleeVector = Quaternion.AngleAxis(Random.Range(-60.0f, 60.0f), Vector3.up) * originalFleeVector;
                }

                if (finalDestination.HasValue)
                {
                    b.FleeFromTarget(_target, finalDestination.Value);
                    b.ShowThought(Thought.Alert);
                    b.PlayEmote(Thought.Surprise);
                }
            }

            public override void OnCollisionStay(PhantomBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantomBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantomBehaviour b)
            {
                if (!b._onGround) return;

                b.SetRoamIfInvalidTarget();
            }

            public override void ExitState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> nextState)
            {
                b.CancelFlee();
            }

            public override void DebugDraw(PhantomBehaviour b)
            {
                DrawCubeOverHead(b, Color.red);

                if (_target != null)
                    XRGizmos.DrawPointer(b.HeadPosition, _target.Position - b.HeadPosition, Color.red, 0.3f);
            }
        }

        private class DirectAttack : BasePhantomState
        {
            private readonly float _duration = 1.0f;
            private float _startTime;

            public override uint GetStateID()
            {
                return (uint)StateID.DirectAttack;
            }

            public override void EnterState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> lastState)
            {
                _startTime = Time.time;
                b.ShowThought(Thought.Angry);
                b.PlayEmote(Thought.Angry);
            }

            public override void OnCollisionStay(PhantomBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantomBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantomBehaviour b)
            {
                if (!b._onGround) return;

                if (!b.SetRoamIfInvalidTarget())
                {
                    var currentTime = Time.time;
                    if (currentTime - _startTime >= _duration)
                    {
                        _startTime = currentTime;
                        b.AttackComplete(this);
                    }
                }
            }

            public override void ExitState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantomBehaviour b)
            {
                DrawCubeOverHead(b, Color.blue);
            }
        }

        private class RangedAttack : BasePhantomState
        {
            private readonly float _duration = 1.5f;
            private float _startTime;

            public override uint GetStateID()
            {
                return (uint)StateID.RangedAttack;
            }

            public override void EnterState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> lastState)
            {
                _startTime = Time.time;
                b.ShowThought(Thought.Angry);
                b.PlayEmote(Thought.Angry);
            }

            public override void OnCollisionStay(PhantomBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantomBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantomBehaviour b)
            {
                if (!b._onGround) return;

                if (!b.SetRoamIfInvalidTarget())
                {
                    var currentTime = Time.time;
                    if (currentTime - _startTime >= _duration)
                    {
                        _startTime = currentTime;
                        b.SpitAtTarget(this);
                    }
                }
            }

            public override void ExitState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantomBehaviour b)
            {
                DrawCubeOverHead(b, Color.cyan);
            }
        }

        private class Pain : BasePhantomState
        {
            public override uint GetStateID()
            {
                return (uint)StateID.Pain;
            }

            public override void EnterState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> lastState)
            {
            }

            public override void OnCollisionStay(PhantomBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantomBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantomBehaviour b)
            {
            }

            public override void ExitState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantomBehaviour b)
            {
                DrawCubeOverHead(b, Color.yellow);
            }
        }

        private class Die : BasePhantomState
        {
            private readonly float _duration = 1.0f;
            private float _elapsedTime;
            private AnimationCurve _spawnCurve;
            private Transform _transform;

            public override uint GetStateID()
            {
                return (uint)StateID.Die;
            }

            public override void EnterState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> lastState)
            {
                b.CancelMovement();
                _elapsedTime = 0.0f;
                _transform = b.transform;
                _spawnCurve = b.spawnCurve;

                _transform.localScale = Vector3.one;
                PhantoGooSfxManager.Instance.PlayPhantomDieVo(b.Position);
            }

            public override void OnCollisionStay(PhantomBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantomBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantomBehaviour b)
            {
                _elapsedTime += Time.deltaTime;
                while (_elapsedTime < _duration)
                {
                    var t = _elapsedTime / _duration;
                    var scale = _spawnCurve.Evaluate(1.0f - t);
                    _transform.localScale = Vector3.one * scale;
                    return;
                }

                // Return to pool for respawning.
                b.ReturnToPool();
            }

            public override void ExitState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantomBehaviour b)
            {
                DrawCubeOverHead(b, Color.black);
            }
        }

        private class Spawn : BasePhantomState
        {
            private readonly float _duration = 1.0f;
            private float _elapsedTime;
            private AnimationCurve _spawnCurve;
            private Transform _transform;

            public override uint GetStateID()
            {
                return (uint)StateID.Spawn;
            }

            public override void EnterState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> lastState)
            {
                b.CancelMovement();
                b.SetInvulnerable(true);
                _elapsedTime = 0.0f;
                _transform = b.transform;
                _spawnCurve = b.spawnCurve;

                _transform.localScale = Vector3.zero;
                PhantoGooSfxManager.Instance.PlayPhantomSpawnVo(b.Position);
            }

            public override void OnCollisionStay(PhantomBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantomBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantomBehaviour b)
            {
                _elapsedTime += Time.deltaTime;
                while (_elapsedTime < _duration)
                {
                    var t = _elapsedTime / _duration;
                    var scale = _spawnCurve.Evaluate(t);
                    _transform.localScale = Vector3.one * scale;
                    return;
                }

                b.SwitchToRoamState();
            }

            public override void ExitState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> nextState)
            {
                b.SetInvulnerable(false);
                _transform.localScale = Vector3.one;
            }

            public override void DebugDraw(PhantomBehaviour b)
            {
                DrawCubeOverHead(b, Color.white);
            }
        }

        private class DemoRoam : BasePhantomState
        {
            public override uint GetStateID()
            {
                return (uint)StateID.DemoRoam;
            }

            public override void EnterState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> lastState)
            {
                b.ShowThought(Thought.Question);
                b.SetRandomDestination();
            }

            public override void OnCollisionStay(PhantomBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantomBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantomBehaviour b)
            {
                if (!b._onGround) return;
            }

            public override void ExitState(PhantomBehaviour b, IEnemyState<PhantomBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantomBehaviour b)
            {
            }
        }
    }
}
