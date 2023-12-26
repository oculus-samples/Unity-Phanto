// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phanto;
using Phanto.Audio.Scripts;
using Phanto.Enemies.DebugScripts;
using PhantoUtils;
using UnityEngine;
using Utilities.XR;

namespace Phantom
{
    /// <summary>
    ///     Represents a behaviour of a Phantom
    /// </summary>
    public partial class PhantomBehaviour : EnemyBehaviour<PhantomBehaviour>
    {

        public enum StateID
        {
            Roam,
            Chase,
            Flee,
            DirectAttack,
            RangedAttack,
            Pain,
            Die,
            Spawn,
            DemoRoam,
            NumStates
        }

        private static float _gravity;

        [SerializeField] private PhantomController phantomController;

        [SerializeField] private StateID initialState = StateID.Roam;

        [SerializeField] private float meleeRange = 0.1f;
        [SerializeField] private float spitRange = 0.3f;

        [SerializeField] private GameObject gooBallPrefab;

        [SerializeField]private float splashDamage = 0.0052f;

        [SerializeField] private AnimationCurve spawnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [SerializeField] private float splashDamageScale = 20.0f;

        [SerializeField] private LayerMask specialLayer;

        [SerializeField] private float specialLayerDamageScale = 40.0f;

        [SerializeField] private bool playSounds = true;

        //TODO: this could be handled on the extinguisher/weapon side of things so that the weapon dictates damage & particle mass/strength
        private readonly List<ParticleCollisionEvent> _pces = new(1024);

        private readonly IEnemyState<PhantomBehaviour>[] _states =
        {
            new Roam(),
            new Chase(),
            new Flee(),
            new DirectAttack(),
            new RangedAttack(),
            new Pain(),
            new Die(),
            new Spawn(),
            new DemoRoam()
        };

        private readonly HashSet<PhantomTarget> _targetsInRange = new();
        private Vector3? _currentDestination;

        private IDamageable[] _damageables;

        private Vector3? _furnitureDestination;
        private bool _onGround = true;

        public Vector3 Position => phantomController.Position;
        public Vector3 HeadPosition => phantomController.HeadPosition;

        public PhantomTarget CurrentTarget { get; private set; }

        public float MeleeRange => meleeRange;
        public float SpitRange => spitRange;

        protected override uint InitialState => (uint)initialState;

        public bool Tutorial
        {
            get;
            internal set;
        } = false;

        protected override void Awake()
        {
            if (_gravity == default) _gravity = Physics.gravity.y;

            e = GetComponent<Enemy>();
        }

        private IEnumerator Start()
        {
            while (!phantomController.Ready) yield return null;

            curState = GetState(InitialState);
            curState.EnterState(this, curState);
        }

        private void OnEnable()
        {
            phantomController.DestinationReached += OnDestinationReached;
            phantomController.PathingFailed += OnPathingFailed;

            _damageables = GetComponents<IDamageable>();
            if (phantomController.Ready)
            {
                e.ResetHealth();
                if (curState is not Spawn) SwitchState(StateID.Spawn);
            }

            DebugDrawManager.DebugDrawEvent += DebugDraw;
        }

        private void OnDisable()
        {
            foreach (var target in _targetsInRange)
            {
                target.Forget -= OnForgetTarget;
            }

            phantomController.DestinationReached -= OnDestinationReached;
            phantomController.PathingFailed -= OnPathingFailed;

            DebugDrawManager.DebugDrawEvent -= DebugDraw;
        }

        private void OnParticleCollision(GameObject other)
        {
            ParticleSystem ps;
            if (!other.TryGetComponent(out ps)) return;

            var count = ps.GetCollisionEvents(gameObject, _pces);

            var accumulatedDamage = 0f;
            var avgPCIntersection = new Vector3();
            var avgPCNormal = new Vector3();
            var sumPCVelocity = new Vector3();

            var isEctoBlaster = specialLayer == (specialLayer | (1 << other.layer));

            for (var i = 0; i < count; i++)
            {
                var pce = _pces[i];

                if (isEctoBlaster) // Additional damage for a special layer (in this case - the blaster)
                {
                    accumulatedDamage += splashDamage * specialLayerDamageScale;
                }
                else
                {
                    accumulatedDamage += splashDamage * splashDamageScale;
                }
                avgPCNormal += pce.normal;
                avgPCIntersection += pce.intersection;
                sumPCVelocity += pce.velocity;
            }

            avgPCIntersection /= count;
            avgPCNormal /= count;

            foreach (var damageable in _damageables)
                damageable.TakeDamage(accumulatedDamage, avgPCIntersection, avgPCNormal);

            if (!isEctoBlaster)
            {
                PolterblastTrigger.DamageNotification(accumulatedDamage, avgPCIntersection, avgPCNormal);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (PhantomTarget.TryGetTarget(other, out var target))
            {
                target.Forget += OnForgetTarget;
                _targetsInRange.Add(target);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (PhantomTarget.TryGetTarget(other, out var target))
            {
                target.Forget -= OnForgetTarget;
                _targetsInRange.Remove(target);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (phantomController == null) phantomController = GetComponent<PhantomController>();
        }
#endif

        private void OnForgetTarget(PhantomTarget target)
        {
            target.Forget -= OnForgetTarget;
            _targetsInRange.Remove(target);

            if (CurrentTarget == target)
            {
                CurrentTarget = null;
                switch (curState)
                {
                    case Flee:
                    case Chase:
                    case DirectAttack:
                    case RangedAttack:
                        SwitchState(StateID.Roam);
                        break;
                }
            }
        }

        public override uint GetNumStates()
        {
            return (uint)StateID.NumStates;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IEnemyState<PhantomBehaviour> GetState(StateID stateID)
        {
            return GetState((uint)stateID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override IEnemyState<PhantomBehaviour> GetState(uint stateID)
        {
            return _states[(int)stateID];
        }

        public override void Heal(float healing, IDamageable.DamageCallback callback = null)
        {
        }

        public override void TakeDamage(float damage, Vector3 position, Vector3 normal,
            IDamageable.DamageCallback callback = null)
        {
            if (e.Health <= 0 || e.invulnerable)
            {
                if (!e.invulnerable && curState is not Die) SwitchState(StateID.Die);

                return;
            }

            // FIXME: The position seems incorrect on some damage events
            var phantomPos = Position;
            var damageVector = position - phantomPos;
            var ray = new Ray(phantomPos, damageVector);
            var ouchPoint = ray.GetPoint(0.02f);

            PhantomManager.Instance.SpawnOuch(ouchPoint);

            switch (curState)
            {
                case Roam roamState:
                    break;
                case Chase chaseState:
                    break;
                case Flee fleeState:
                    break;
                case DirectAttack directAttackState:
                    // Flinch?
                    break;
                case RangedAttack rangedAttackState:
                    // Flinch?
                    break;
                case Pain painState:
                    break;
            }
        }

        public void SetOnGround(bool onGround)
        {
            if (!onGround && playSounds) PhantoGooSfxManager.Instance.PlayMinionJumpVo(Position);

            _onGround = onGround;
        }

        private void SetInvulnerable(bool invulnerable)
        {
            e.invulnerable = invulnerable;
        }

        private void SwitchState(StateID nextStateID)
        {
            SwitchState((uint)nextStateID);
        }

        private void CheckForTargets()
        {
            // Check neighborhood for attack/flee targets that would change state
            if (_targetsInRange.Count > 0 && _onGround) SelectTarget();
        }

        private void SelectTarget()
        {
            PhantomTarget closest = null;
            var minDistance = float.MaxValue;

            var position = phantomController.Position;

            foreach (var target in _targetsInRange)
            {
                if (!target.Valid) continue;

                var distance = Vector3.Distance(target.Position, position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = target;
                }
            }

            if (closest == null) return;

            CurrentTarget = closest;

            // selected behavior will be running away.
            if (CurrentTarget.Flee)
                SwitchState(StateID.Flee);
            else if (!Tutorial)
                SwitchState(StateID.Chase);
        }

        private void OnPathingFailed()
        {
            IEnumerator WaitAFrame()
            {
                // FIXME: Agents are starting before NavMesh is generated.
                yield return null;
                EvaluateState();
            }

            StartCoroutine(WaitAFrame());
        }

        private void OnDestinationReached()
        {
            EvaluateState();
        }

        private void EvaluateState()
        {
            switch (curState)
            {
                case Roam roamState:
                case Flee fleeState:
                    if (_targetsInRange.Count > 0)
                        SelectTarget();
                    else
                        // Pick another random location.
                        SwitchState(StateID.Roam);

                    break;
                case Chase chaseState:
                    // close enough to attack?
                    var target = _currentDestination.GetValueOrDefault(CurrentTarget.Position);

                    // 2D distance to target.
                    var delta = target - Position;

                    var range3d = delta.magnitude;
                    var range2d = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;

                    switch (CurrentTarget)
                    {
                        case RangedFurnitureTarget _:
                            if (range2d <= spitRange)
                                SwitchState(StateID.RangedAttack);
                            else
                                SwitchState(StateID.Roam);

                            break;
                        default:
                            if (range3d <= meleeRange)
                                SwitchState(StateID.DirectAttack);
                            else if (range2d <= spitRange)
                                SwitchState(StateID.RangedAttack);
                            else
                                SwitchState(StateID.Roam);

                            break;
                    }

                    break;
                case DemoRoam demoRoam:
                    // Pick another random location.
                    SwitchState(StateID.DemoRoam);
                    break;
            }
        }

        private bool TargetIsValid()
        {
            if (CurrentTarget == null) return false;

            if (!CurrentTarget.Valid)
            {
                CurrentTarget = null;
                return false;
            }

            // check if we've wandered near an ouch beacon
            foreach (var target in _targetsInRange)
            {
                // don't attempt to flee if you're invulnerable or airborne.
                if (target == CurrentTarget || !target.Flee || e.invulnerable || !_onGround) continue;

                CurrentTarget = target;
                SwitchState(StateID.Flee);
                return true;
            }

            return true;
        }

        private bool SetRoamIfInvalidTarget()
        {
            if (!TargetIsValid())
            {
                SwitchState(StateID.Roam);
                return true;
            }

            return false;
        }

        private void AttackComplete(IEnemyState<PhantomBehaviour> attack)
        {
            phantomController.AttackAnimation();
            CurrentTarget.TakeDamage(1.0f);

            if (playSounds) PhantoGooSfxManager.Instance.PlayMinionLaughVo(Position);
        }

        private void SpitAtTarget(IEnemyState<PhantomBehaviour> attack)
        {
            phantomController.AttackAnimation();
            var targetPoint = CurrentTarget.GetAttackPoint();
            var launchPoint = HeadPosition;

            var (launchVector, _) = PhantomController.CalculateHopVelocity(launchPoint, targetPoint, 0.1f, _gravity);

            var projectile = PoolManagerSingleton.Instance.Create(gooBallPrefab,
                HeadPosition,
                transform.rotation);

            projectile.GetComponent<Rigidbody>().LaunchProjectile(launchPoint, launchVector);

            CurrentTarget.TakeDamage(1.0f);

            if (playSounds) PhantoGooSfxManager.Instance.PlayMinionLaughVo(Position);
        }

        private void SetDestination(Vector3 destination)
        {
            _currentDestination = destination;
            phantomController.SetDestination(destination);

            if (CurrentTarget is RangedFurnitureTarget)
                _furnitureDestination = destination;
            else
                _furnitureDestination = null;
        }

        private void FleeFromTarget(PhantomTarget target, Vector3 destination)
        {
            // don't consider this target again until we re-enter its trigger.
            _targetsInRange.Remove(target);

            phantomController.Fleeing(true);
            phantomController.SetDestination(destination);
        }

        private void CancelFlee()
        {
            phantomController.Fleeing(false);
        }

        private void SetRandomDestination()
        {
            phantomController.SetRandomDestination();
        }

        private void CancelMovement()
        {
            phantomController.ClearPath();
        }

        private void ReturnToPool()
        {
            phantomController.ReturnToPool();
        }

        private void DebugDraw()
        {
            if (curState is BasePhantomState phantomState) phantomState.DebugDraw(this);

            if (_furnitureDestination.HasValue) XRGizmos.DrawPoint(_furnitureDestination.Value, MSPalette.HotPink);
        }
    }
}
