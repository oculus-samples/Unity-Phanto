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
using Debug = UnityEngine.Debug;

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
            CrystalRoam,
            CrystalChase,
            NumStates
        }

        private static float _gravity;

        [SerializeField] private PhantomController phantomController;

        [SerializeField] private StateID initialState = StateID.Roam;

        [SerializeField] private float meleeRange = 0.1f;
        [SerializeField] private float spitRange = 0.3f;

        [SerializeField] private GameObject gooBallPrefab;

        [SerializeField] private float splashDamage = 0.0052f;

        [SerializeField] private AnimationCurve spawnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [SerializeField] private float splashDamageScale = 20.0f;

        [SerializeField] private LayerMask specialLayer;

        [SerializeField] private float specialLayerDamageScale = 40.0f;

        [SerializeField] private bool playSounds = true;

        [SerializeField] private ThoughtBubbleController thoughtBubbleController;

        //TODO: this could be handled on the extinguisher/weapon side of things so that the weapon dictates damage & particle mass/strength
        private readonly List<ParticleCollisionEvent> _pces = new(1024);

        private PhantomTarget _previousTarget;

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
            new DemoRoam(),
            new CrystalRoam(),
            new CrystalChase(),
        };

        private readonly HashSet<PhantomTarget> _targetsInRange = new();
        private Vector3? _currentDestination;

        private IDamageable[] _damageables;

        private Vector3? _furnitureDestination;
        private bool _onGround = true;

        private int _launchesWithoutHit = 0;
        private PhantoGooSfxManager _sfxManager;
        private bool _sfxManagerReady = false;

        public Vector3 Position => phantomController.Position;
        public Vector3 HeadPosition => phantomController.HeadPosition;

        public PhantomTarget CurrentTarget { get; private set; }

        public float MeleeRange => meleeRange;
        public float SpitRange => spitRange;

        protected override uint InitialState => (uint)initialState;

        public bool Tutorial { get; internal set; } = false;

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
            _sfxManager = PhantoGooSfxManager.Instance;
            _sfxManagerReady = _sfxManager != null;

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

            _launchesWithoutHit = 0;
        }

        private void OnParticleCollision(GameObject other)
        {
            if (!other.TryGetComponent(out ParticleSystem ps)) return;

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
                damageable.TakeDamage(accumulatedDamage, avgPCIntersection, avgPCNormal, other);

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
                    case CrystalChase:
                    case DirectAttack:
                    case RangedAttack:
                        SwitchToRoamState();
                        break;
                }
            }

            _launchesWithoutHit = 0;
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
            GameObject damageSource = null, IDamageable.DamageCallback callback = null)
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

            if (PolterblastTrigger.TryGetPolterblaster(damageSource, out var trigger))
            {
                phantomController.SpawnOuch(ouchPoint, phantomPos - trigger.Position);
            }

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
            if (!onGround && playSounds && _sfxManagerReady) _sfxManager.PlayPhantomJumpVo(Position);

            _onGround = onGround;
        }

        public void ResetState()
        {
            if (CurrentTarget != null)
            {
                CurrentTarget.Forget -= OnForgetTarget;
            }

            CurrentTarget = null;

            foreach (var target in _targetsInRange)
            {
                target.Forget -= OnForgetTarget;
            }

            _targetsInRange.Clear();

            SwitchToRoamState();
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
            var winCondition = phantomController.WinCondition;

            switch (winCondition)
            {
                case GameplaySettings.WinCondition.DefeatPhanto:
                    // Check neighborhood for attack/flee targets that would change state
                    if (_targetsInRange.Count > 0 && _onGround) SelectTarget();
                    break;
                case GameplaySettings.WinCondition.DefeatAllPhantoms:
                    if (CurrentTarget != null && CurrentTarget.Valid)
                    {
                        SwitchState(StateID.CrystalChase);
                    }
                    else
                    {
                        phantomController.RequestCrystalTarget();
                    }
                    break;
            }
        }

        private void SelectTarget()
        {
            var position = phantomController.Position;
            PhantomTarget target = null;

            target = ClosestDetectedTarget(position);

            if (target == null) return;

            switch (target)
            {
                case PhantomFleeTarget fleeTarget:
                    // so we can remember what we were doing before getting scared.
                    if (CurrentTarget is not PhantomFleeTarget)
                    {
                        _previousTarget = CurrentTarget;
                    }

                    CurrentTarget = fleeTarget;

                    SwitchState(StateID.Flee);
                    break;
                default:
                    CurrentTarget = target;

                    if (!Tutorial)
                        SwitchState(StateID.Chase);
                    break;
            }
        }

        private PhantomTarget ClosestDetectedTarget(Vector3 position)
        {
            float minDistance = float.MaxValue;
            PhantomTarget closest = null;
            foreach (var target in _targetsInRange)
            {
                if (target==null || !target.Valid) continue;

                var distance = Vector3.Distance(target.Position, position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = target;
                }
            }

            return closest;
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

        /// <summary>
        ///
        /// </summary>
        /// <param name="inRange">if true phantom should be able to attack its target</param>
        private void OnDestinationReached(bool inRange)
        {
            EvaluateState();
        }

        private void EvaluateState()
        {
            var winCondition = phantomController.WinCondition;

            Vector3 target;
            Vector3 delta;
            float range3d;
            float range2d;

            switch (curState)
            {
                case Roam roamState:
                case Flee fleeState:
                    switch (winCondition)
                    {
                        case GameplaySettings.WinCondition.DefeatPhanto:
                            if (_targetsInRange.Count > 0)
                            {
                                SelectTarget();
                            }
                            else
                            {
                                SwitchToRoamState();
                            }
                            break;
                        case GameplaySettings.WinCondition.DefeatAllPhantoms:
                            SwitchToRoamState();
                            break;
                    }
                    break;
                case Chase chaseState:
                    // close enough to attack?
                    target = _currentDestination.GetValueOrDefault(CurrentTarget.Position);

                    // 2D distance to target.
                    delta = target - Position;

                    range3d = delta.magnitude;
                    range2d = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;

                    switch (CurrentTarget)
                    {
                        case RangedFurnitureTarget _:
                            if (range2d <= spitRange)
                                SwitchState(StateID.RangedAttack);
                            else
                                SwitchToRoamState();

                            break;
                        default:
                            if (range3d <= meleeRange)
                                SwitchState(StateID.DirectAttack);
                            else if (range2d <= spitRange)
                                SwitchState(StateID.RangedAttack);
                            else
                                SwitchToRoamState();
                            break;
                    }

                    break;
                case DemoRoam demoRoam:
                    // Pick another random location.
                    SwitchState(StateID.DemoRoam);
                    break;
                case CrystalRoam crystalRoam:
                    // this state shouldn't do anything except select a crystal.
                    break;
                case CrystalChase crystalChase:
                    // close enough to attack?
                    target = _currentDestination.GetValueOrDefault(CurrentTarget.Position);

                    // 2D distance to target.
                    delta = target - Position;

                    range3d = delta.magnitude;
                    range2d = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;

                    switch (CurrentTarget)
                    {
                        case RangedFurnitureTarget _:
                            if (range2d <= spitRange)
                                SwitchState(StateID.RangedAttack);
                            else
                                SwitchToRoamState();

                            break;
                        default:
                            if (range3d <= meleeRange)
                                SwitchState(StateID.DirectAttack);
                            else if (range2d <= spitRange)
                                SwitchState(StateID.RangedAttack);
                            else
                                SwitchToRoamState();
                            break;
                    }

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

                if (CurrentTarget is not PhantomFleeTarget)
                {
                    _previousTarget = CurrentTarget;
                }

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
                SwitchToRoamState();
                return true;
            }

            return false;
        }

        private void SwitchToRoamState()
        {
            switch (phantomController.WinCondition)
            {
                case GameplaySettings.WinCondition.DefeatPhanto:
                    SwitchState(StateID.Roam);
                    break;
                case GameplaySettings.WinCondition.DefeatAllPhantoms:
                    SwitchState(StateID.CrystalRoam);
                    _launchesWithoutHit = 0;
                    break;
            }
        }

        public void SetCrystalTarget(PhantomTarget target)
        {
            if (CurrentTarget == target)
            {
                return;
            }

            if (CurrentTarget != null)
            {
                OnForgetTarget(CurrentTarget);
            }

            CurrentTarget = target;
            _launchesWithoutHit = 0;
        }

        public void SuccessfulLaunch()
        {
            // hit your target, so reset the count.
            _launchesWithoutHit = 0;
        }

        private void AttackComplete(IEnemyState<PhantomBehaviour> attack)
        {
            phantomController.AttackAnimation();
            CurrentTarget.TakeDamage(1.0f);

            if (playSounds && _sfxManagerReady) _sfxManager.PlayPhantomLaughVo(Position);
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

            if (projectile.TryGetComponent<PhantoGooBall>(out var gooBall))
            {
                var winCondition = phantomController.WinCondition;

                // pass win condition to the projectile to control its behavior.
                gooBall.LaunchProjectile(launchPoint, launchVector, winCondition, this);

                switch (winCondition)
                {
                    case GameplaySettings.WinCondition.DefeatAllPhantoms:
                        _launchesWithoutHit++;

                        if (_launchesWithoutHit > 4)
                        {
                            // probably can't hit the target from here.
                            // pick a new standing position.
                            SwitchToRoamState();
                        }
                        break;
                    default:
                        break;
                }
            }
            else
            {
                Debug.LogError("projectile prefab has no PhantoGooBall component,");
            }

            CurrentTarget.TakeDamage(1.0f);

            if (playSounds && _sfxManagerReady) _sfxManager.PlayPhantomLaughVo(Position);
        }

        private void SetDestination(PhantomTarget target, Vector3 destination)
        {
            _currentDestination = destination;
            phantomController.SetDestination(target, destination);

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
            phantomController.SetDestination(target, destination);
        }

        private void CancelFlee()
        {
            CurrentTarget = _previousTarget;

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
            phantomController.DecrementPhantom();
        }

        private void ShowThought(Thought thought)
        {
            thoughtBubbleController.ShowThought(thought);
            if (playSounds && _sfxManagerReady) _sfxManager.PlayPhantomThoughtBubbleAppear(transform.position);
        }

        private void PlayEmote(Thought thought)
        {
            phantomController.PlayEmote(thought);
        }

        private void ShowTargetThought(PhantomTarget target)
        {
            Thought thought = Thought.None;
            string label = null;

            switch (target)
            {
                case PhantomChaseTarget chaseTarget:
                    if (SceneQuery.TryGetClosestSemanticClassification(chaseTarget.GetAttackPoint(), Vector3.up, out var classification))
                    {
                        label = classification.Labels[0];
                    }
                    break;
                case RangedFurnitureTarget furnitureTarget:
                    label = furnitureTarget.Classification;
                    break;
                case PhantomFleeTarget fleeTarget:
                    thoughtBubbleController.ShowThought(Thought.Alert);
                    return;
            }

            switch (label)
            {
                case OVRSceneManager.Classification.Couch:
                    thought = Thought.Couch;
                    break;
                case OVRSceneManager.Classification.Other:
                    thought = Thought.Other;
                    break;
                case OVRSceneManager.Classification.Storage:
                    thought = Thought.Storage;
                    break;
                case OVRSceneManager.Classification.Bed:
                    thought = Thought.Bed;
                    break;
                case OVRSceneManager.Classification.Table:
                    thought = Thought.Table;
                    break;
                case OVRSceneManager.Classification.DoorFrame:
                    thought = Thought.Door;
                    break;
                case OVRSceneManager.Classification.WindowFrame:
                    thought = Thought.Window;
                    break;
                case OVRSceneManager.Classification.Screen:
                    thought = Thought.Screen;
                    break;
                case OVRSceneManager.Classification.Lamp:
                    thought = Thought.Lamp;
                    break;
                case OVRSceneManager.Classification.Plant:
                    thought = Thought.Plant;
                    break;
                case OVRSceneManager.Classification.WallArt:
                    thought = Thought.WallArt;
                    break;
                default:
                    thought = Thought.Angry;
                    break;
            }

            thoughtBubbleController.ShowThought(thought);
        }

        private void DebugDraw()
        {
            if (curState is BasePhantomState phantomState) phantomState.DebugDraw(this);

            if (_furnitureDestination.HasValue) XRGizmos.DrawPoint(_furnitureDestination.Value, MSPalette.HotPink);
        }
    }
}
