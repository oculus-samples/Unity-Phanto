// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using System.Diagnostics;
using Phanto;
using Phanto.Audio.Scripts;
using Phanto.Enemies.DebugScripts;
using Phantom.Environment.Scripts;
using PhantoUtils;
using PhantoUtils.VR;
using UnityEngine;
using Utilities.XR;
using Debug = UnityEngine.Debug;

namespace Phanto
{
    /// <summary>
    /// Base class for Phanto behaviours.
    /// </summary>
    public class PhantoBehaviour : EnemyBehaviour<PhantoBehaviour>
    {
        // Phanto's states
        public enum StateID : uint
        {
            Nova,
            SpitGooBall,
            Pain,
            Dodge,
            GoEthereal,
            Roam,
            Die,
            DemoRoam,
            NumStates
        }

        [SerializeField] private StateID initialState = StateID.Roam;
        private float _outOfBoundsDuration;

        private Bounds _sceneBounds;
        private SphereCollider _sphereCollider;

        private Transform _transform;
        private readonly Transform lookTarget = null;

        // States
        private readonly IEnemyState<PhantoBehaviour>[] states =
        {
            new Nova(),
            new SpitGooBall(),
            new Pain(),
            new Dodge(),
            new GoEthereal(),
            new Roam(),
            new Die(),
            new DemoRoam()
        };

        private Vector3 targetPos;

        protected override uint InitialState => (uint)initialState;

        public Vector3 Position => _transform.position;

        protected override void Awake()
        {
            gameObject.SetActive(false);
            _transform = transform;
            _sphereCollider = GetComponent<SphereCollider>();
            base.Awake();
        }

        private void OnEnable()
        {
            SceneBoundsChecker.BoundsChanged += OnBoundsChanged;

            DebugDrawManager.DebugDrawEvent += DebugDraw;
        }

        private void OnDisable()
        {
            SceneBoundsChecker.BoundsChanged -= OnBoundsChanged;

            DebugDrawManager.DebugDrawEvent -= DebugDraw;
        }

        private void OnBoundsChanged(Bounds bounds)
        {
            _sceneBounds = bounds;
            _sceneBounds.Expand(NavMeshConstants.OneFoot);
        }

        public override uint GetNumStates()
        {
            return (uint)StateID.NumStates;
        }

        public void SwitchState(StateID nextStateID)
        {
            SwitchState((uint)nextStateID);
        }

        public override IEnemyState<PhantoBehaviour> GetState(uint stateID)
        {
            return states[(int)stateID];
        }

        public override void Heal(float healing, IDamageable.DamageCallback callback = null)
        {
        }

        /// <summary>
        /// Called when an enemy collides with another object
        /// </summary>
        public override void TakeDamage(float damage, Vector3 position, Vector3 normal,
            IDamageable.DamageCallback callback = null)
        {
            if ((e.Health <= 0) |
                e.invulnerable)
                return;

            Phanto.Instance.SetEmissionColor(Random.Range(0.25f, 6f) * new Color(0.77f, 0.88f, 1f));

            if (curState.GetStateID() == (int)StateID.Roam)
            {
                if (Pain.CausesPain(this, damage)) ((Roam)curState).FeelPain();
            }
            else if (curState.GetStateID() != (int)StateID.Nova &&
                     curState.GetStateID() != (int)StateID.Pain &&
                     curState.GetStateID() != (int)StateID.Dodge &&
                     Dodge.ShouldDodge())
            {
                SwitchState(StateID.Dodge);
            }
            else if (curState.GetStateID() != (int)StateID.Pain &&
                     curState.GetStateID() != (int)StateID.Dodge &&
                     Pain.CausesPain(this, damage) &&
                     (curState.GetStateID() != (int)StateID.Nova ||
                      ((Nova)curState).CanInterrupt()))
            {
                SwitchState(StateID.Pain);
            }
        }

        internal void OnTargetDeath()
        {
            SwitchState(StateID.Roam);
        }

        private void CheckInBounds()
        {
            if (_sceneBounds.Contains(_transform.position))
            {
                _outOfBoundsDuration = 0.0f;
                return;
            }

            _outOfBoundsDuration += Time.deltaTime;

            if (_outOfBoundsDuration > 3.0f)
            {
                var point = NavMeshGenerator.Instance.RandomPointOnFloor(CameraRig.Instance.CenterEyeAnchor.position);
                point.y = _sceneBounds.center.y;
                _transform.position = point;
            }
        }

        protected override void UpdateBehaviour()
        {
            CheckInBounds();

            Phanto.Instance.SetEmissionColor(new Color(1f, 1f, 1f, 1f));

            if (e.Health <= 0f)
            {
                Phanto.Instance.AdvanceWave();
                if (Phanto.Instance.Wave < Phanto.MAX_WAVES)
                    SwitchState(StateID.GoEthereal);
                else if (curState.GetStateID() != (int)StateID.Die) SwitchState(StateID.Die);
            }

            base.UpdateBehaviour();
        }

        private void DebugDraw()
        {
            if (curState is BasePhantoState phantoState) phantoState.DebugDraw(this);
            XRGizmos.DrawCollider(_sphereCollider, Color.white);
        }

        #region States

        /// <summary>
        /// The "Nova" state is a simple "spitfire".
        /// </summary>
        private class Nova : BasePhantoState
        {
            public const float MIN_SQR_GOO_DIST = 0.04f;

            private static readonly float[] preCastTimePerWave = { 2f, 1f, 0.5f };
            private static readonly float[] castTimePerWave = { 5f, 3f, 2f };
            private static readonly float[] postCastTimePerWave = { 0.3f, 0.25f, 0.05f };

            private static readonly int[] numTrailsPerWave = { 3, 4, 6 };

            private Coroutine novaCoroutine;
            private bool recovering;

            private float timer;

            public override uint GetStateID()
            {
                return (uint)StateID.Nova;
            }

            public bool CanInterrupt()
            {
                return !recovering | (timer <= postCastTimePerWave[Phanto.Instance.Wave]);
            }

            public override void EnterState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> lastState)
            {
                timer = preCastTimePerWave[Phanto.Instance.Wave];
                recovering = false;

                b.targetPos = b.e.rigidbody.position;

                if (b.e.invulnerable)
                    b.e.StartCoroutine(Phanto.Instance.ToggleEthereal(preCastTimePerWave[Phanto.Instance.Wave]));
            }

            public override void OnCollisionStay(PhantoBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantoBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantoBehaviour b)
            {
                b.e.HoverAround(b.targetPos);
                b.e.AimTowards(b.lookTarget != null ? b.lookTarget.position : Vector3.zero);
                b.e.KeepUpright(Vector3.up);

                if (timer <= 0f)
                {
                    if (recovering)
                    {
                        b.SwitchState(StateID.Roam);
                        return;
                    }

                    Phanto.Instance.animator.SetTrigger(Phanto.spitParamId);
                    Phanto.Instance.animator.speed = Phanto.Instance.animator.GetCurrentAnimatorStateInfo(0).length /
                                                     castTimePerWave[Phanto.Instance.Wave];

                    novaCoroutine = b.e.StartCoroutine(Phanto.Instance.CastGooNova(
                        numTrailsPerWave[Phanto.Instance.Wave],
                        MIN_SQR_GOO_DIST,
                        castTimePerWave[Phanto.Instance.Wave],
                        LayerMask.GetMask("GlobalMesh")));
                    recovering = true;
                    timer = castTimePerWave[Phanto.Instance.Wave] + postCastTimePerWave[Phanto.Instance.Wave];
                }
                else
                {
                    timer -= Time.fixedDeltaTime;
                }
            }

            public override void ExitState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> nextState)
            {
                if (novaCoroutine != null) b.e.StopCoroutine(novaCoroutine);

                Phanto.Instance.animator.speed = 1f;
                b.e.ResetMovementSettings();
            }

            public override void DebugDraw(PhantoBehaviour b)
            {
                DrawStateCube(b, Color.magenta);
            }
        }

        /// <summary>
        /// The "Spit Goo Ball" state.
        /// </summary>
        private class SpitGooBall : BasePhantoState
        {
            private static readonly Vector2[] weaponSpreadPerWave =
            {
                new(0.01f, 0.05f),
                new(0.02f, 0.06f),
                new(0.035f, 0.07f)
            };

            private static readonly uint[] weaponNumProjPerWave = { 2u, 3u, 1u };
            private static readonly float[] preAttackTimePerWave = { 0.16f, 0.08f, 0.04f };
            private static readonly float[] refireTimePerWave = { 0.5f, 0.2f, 0.1f };
            private static readonly float[] postAttackTimePerWave = { 0.3f, 0.25f, 0.05f };
            private static readonly int[] numVolleysPerWave = { 1, 2, 3 };
            private float timer;

            private int volley;

            public override uint GetStateID()
            {
                return (uint)StateID.SpitGooBall;
            }

            public override void EnterState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> lastState)
            {
                volley = 0;
                timer = preAttackTimePerWave[Phanto.Instance.Wave];
                b.targetPos = b.e.rigidbody.position;
                b.e.maxAngularVelocity = 0.3f * b.e.maxAngularVelocity;
                b.e.maxAngularAcceleration = 0.2f * b.e.maxAngularAcceleration;
            }

            public override void OnCollisionStay(PhantoBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantoBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantoBehaviour b)
            {
                b.e.HoverAround(b.targetPos);
                b.e.AimTowards(b.lookTarget != null ? b.lookTarget.position : Vector3.zero);
                b.e.KeepUpright(Vector3.up);

                if (timer <= 0f)
                {
                    if (volley >= numVolleysPerWave[Phanto.Instance.Wave])
                    {
                        b.SwitchState(StateID.Roam);
                        return;
                    }

                    if (Phanto.Instance.Wave >= 0 && Phanto.Instance.Wave <= weaponNumProjPerWave.Length)
                        if (Phanto.Instance.Wave >= 0 && Phanto.Instance.Wave <= weaponSpreadPerWave.Length)
                            Phanto.Instance.ShootGooball(weaponNumProjPerWave[Phanto.Instance.Wave],
                                weaponSpreadPerWave[Phanto.Instance.Wave]);

                    if (volley < numVolleysPerWave[Phanto.Instance.Wave])
                    {
                        ++volley;
                        timer = refireTimePerWave[Phanto.Instance.Wave];
                    }
                    else
                    {
                        timer = postAttackTimePerWave[Phanto.Instance.Wave];
                    }
                }
                else
                {
                    timer -= Time.fixedDeltaTime;
                }
            }

            public override void ExitState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> nextState)
            {
                b.e.ResetMovementSettings();
            }

            public override void DebugDraw(PhantoBehaviour b)
            {
                DrawStateCube(b, Color.red);
            }
        }

        /// <summary>
        /// The "Pain" state.
        /// </summary>
        private class Pain : BasePhantoState
        {
            private static readonly float[] painChancePerWave = { 0.1f, 0.07f, 0.04f };
            private static readonly float[] painTimePerWave = { 0.11f, 0.08f, 0.0666667f };
            private float timeLeft;

            public static bool CausesPain(PhantoBehaviour b, float damage)
            {
                return damage >= b.e.painThreshold &&
                       Random.value <= painChancePerWave[Phanto.Instance.Wave];
            }

            public override uint GetStateID()
            {
                return (uint)StateID.Pain;
            }

            public override void EnterState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> lastState)
            {
                timeLeft = painTimePerWave[Phanto.Instance.Wave];
                Phanto.Instance.animator.SetTrigger(Phanto.hitParamId);
            }

            public override void OnCollisionStay(PhantoBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantoBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantoBehaviour b)
            {
                b.e.HoverAround(b.targetPos);
                b.e.AimTowards(b.lookTarget != null ? b.lookTarget.position : Vector3.zero);
                b.e.KeepUpright(Vector3.up);

                timeLeft -= Time.fixedDeltaTime;
                if (timeLeft <= 0f) b.SwitchState(StateID.Roam);
            }

            public override void ExitState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantoBehaviour b)
            {
                DrawStateCube(b, Color.blue);
            }
        }

        /// <summary>
        /// The "Go Ethereal" state.
        /// </summary>
        private class GoEthereal : BasePhantoState
        {
            private static readonly float[] castTimePerWave = { 3f, 2f, 1f };
            private bool spinCW;

            private float timeLeft;

            public override uint GetStateID()
            {
                return (uint)StateID.GoEthereal;
            }

            public override void EnterState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> lastState)
            {
                PhantoGooSfxManager.Instance.PlayPhantoDieVo(b.transform.position);
                timeLeft = castTimePerWave[Phanto.Instance.Wave];
                spinCW = Random.value < 0.5f;
                b.targetPos = b.e.rigidbody.position;
                b.e.ResetHealth();
                if (b.e.isActiveAndEnabled)
                {
                    b.e.StartCoroutine(Phanto.Instance.ToggleEthereal(castTimePerWave[Phanto.Instance.Wave]));
                }
            }

            public override void OnCollisionStay(PhantoBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantoBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantoBehaviour b)
            {
                b.e.HoverAround(b.targetPos);
                b.e.Spin(spinCW);
                b.e.KeepUpright(Vector3.up);

                timeLeft -= Time.fixedDeltaTime;
                if (timeLeft <= 0f) b.SwitchState(StateID.Roam);
            }

            public override void ExitState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantoBehaviour b)
            {
                DrawStateCube(b, Color.cyan);
            }
        }

        /// <summary>
        /// The "Dodge" state.
        /// </summary>
        private class Dodge : BasePhantoState
        {
            public const float X_DIST = 0.3f;
            private static readonly float[] dodgeChancePerWave = { 0.3f, 0.6f, 0.9f };
            private bool left;

            public static bool ShouldDodge()
            {
                return Random.value <= dodgeChancePerWave[Phanto.Instance.Wave];
            }

            public override uint GetStateID()
            {
                return (uint)StateID.Dodge;
            }

            public override void EnterState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> lastState)
            {
                left = Random.value < 0.5f;
                b.targetPos = b.e.rigidbody.position;
                var rot = Quaternion.AngleAxis(b.e.rigidbody.rotation.eulerAngles.y, Vector3.up);
                if (b.targetPos.y >= 0.75f &&
                    Random.value <= 0.5f)
                {
                    b.targetPos.y = Random.Range(0.45f, Mathf.Max(0.45f, 0.9f * b.targetPos.y - 0.46f));
                    b.targetPos += rot * (X_DIST * (left ? Vector3.left : Vector3.right));
                }
                else
                {
                    b.targetPos += X_DIST * (left ? Vector3.up : Vector3.down);
                    b.targetPos += rot * (Random.Range(2f * X_DIST, 3.75f * X_DIST) *
                                          (left ? Vector3.left : Vector3.right));
                }
            }

            public override void OnCollisionStay(PhantoBehaviour b, Collision c)
            {
                var contact = PhysicsUtils.GetAverageContact(c);
                var cLocal = contact - b.e.rigidbody.position;
                var cDir = cLocal.normalized;
                b.e.MoveAlong(-cDir);

                var tLocal = b.targetPos - b.e.rigidbody.position;
                if (Vector3.Dot(tLocal, cDir) >= cLocal.magnitude - b.e.proxSensor.Radius)
                {
                    cLocal = contact - b.targetPos;
                    b.targetPos += 1.3f * cLocal + b.e.proxSensor.Radius * cLocal.normalized;
                }
            }

            public override void OnProximityStay(PhantoBehaviour b, Collider c)
            {
            }

            public override void UpdateState(PhantoBehaviour b)
            {
                b.e.BarrelRoll(left);
                b.e.AimTowards(b.targetPos);
                b.e.FlyTo(b.targetPos);

                if (b.e.rigidbody.position.y - b.targetPos.y <= 0.05f)
                {
                    b.targetPos = b.e.rigidbody.position;
                    b.SwitchState(StateID.Roam);
                }
            }

            public override void ExitState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantoBehaviour b)
            {
                DrawStateCube(b, Color.yellow);
            }
        }

        /// <summary>
        /// The "Roam" state.
        /// </summary>
        private class Roam : BasePhantoState
        {
            private static readonly float[] thinkTimePerWave = { 0.6f, 0.2f, 0.083f };
            private static readonly float[] speedScalePerWave = { 0.3f, 0.5f, 0.7f };
            private static readonly float[] painTimePerWave = { 1.5f, 2f, 2.5f };
            private Vector3 dir;
            private float dirTimer;
            private float painTimer;
            private float thinkTimer;

            public override uint GetStateID()
            {
                return (uint)StateID.Roam;
            }

            public void FeelPain()
            {
                painTimer = painTimePerWave[Phanto.Instance.Wave];
                Phanto.Instance.animator.SetTrigger(Phanto.hitParamId);
                Phanto.Instance.animator.speed = Phanto.Instance.animator.GetCurrentAnimatorStateInfo(0).length /
                                                 painTimePerWave[Phanto.Instance.Wave];
                PhantoGooSfxManager.Instance.PlayPhantoHurtVo(Phanto.Instance.transform.position);
            }

            private void UpdateMoveSpeed(PhantoBehaviour b)
            {
                b.e.ResetMovementSettings();
                var t = Mathf.Min(1f,
                    b.e.Health / 100f + (1f - painTimer / painTimePerWave[Phanto.Instance.Wave]));
                b.e.maxVelocity *= Mathf.Lerp(1f,
                    speedScalePerWave[Phanto.Instance.Wave],
                    t);
                if (Phanto.Instance.animator.GetCurrentAnimatorClipInfo(0).Length > 0)
                    if (!Phanto.Instance.animator.GetCurrentAnimatorClipInfo(0)[0].clip.name.Equals(Phanto.HIT))
                        Phanto.Instance.animator.speed = Mathf.Min(1f / t, 4f);
            }

            public override void EnterState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> lastState)
            {
                UpdateMoveSpeed(b);

                dirTimer = Random.Range(0.067f, 0.2f);
                dir = Quaternion.AngleAxis(Random.Range(-180f, 180f) + b.e.rigidbody.rotation.eulerAngles.y,
                    Vector3.up) * Vector3.forward;

                thinkTimer = b.e.invulnerable ? 6f : thinkTimePerWave[Phanto.Instance.Wave];
            }

            public override void OnCollisionStay(PhantoBehaviour b, Collision c)
            {
                dir = (b.e.rigidbody.position - PhysicsUtils.GetAverageContact(c)).normalized;
                b.e.MoveAlong(dir);
                b.e.AimAlong(dir);
            }

            public override void OnProximityStay(PhantoBehaviour b, Collider c)
            {
                if (!PersonalBubble.IsPlayerBubble(c)) return;

                // if phanto touches the capsule floating over the player it will turn away
                var rigidbodyPosition = b.e.rigidbody.position;
                var colliderPoint = c.ClosestPoint(rigidbodyPosition);

                dir = (rigidbodyPosition - colliderPoint).normalized;
                b.e.MoveAlong(dir);
                b.e.AimAlong(dir);
            }

            public override void UpdateState(PhantoBehaviour b)
            {
                UpdateMoveSpeed(b);

                Debug.DrawRay(b.e.rigidbody.position, dir, Color.yellow);
                b.e.MoveAlong(b.e.transform.TransformDirection(Vector3.forward));
                b.e.AimAlong(dir);
                b.e.KeepUpright(Vector3.up);

                if (painTimer >= 0f) painTimer -= Time.fixedDeltaTime;

                if (dirTimer <= 0f)
                {
                    dirTimer = Random.Range(0.067f, 0.2f);
                    dir = Quaternion.AngleAxis(Random.Range(-30f, 30f) + b.e.rigidbody.rotation.eulerAngles.y,
                        Vector3.up) * Vector3.forward;
                }
                else
                {
                    dirTimer -= Time.fixedDeltaTime;
                }

                if (thinkTimer <= 0f)
                {
                    if (b.e.invulnerable)
                    {
                        b.SwitchState(StateID.Nova);
                        return;
                    }

                    var strategy = (Strategy)Random.Range(0, (int)Strategy.NumStrats);
                    switch (strategy)
                    {
                        case Strategy.SpitGooBall:
                            b.SwitchState(StateID.SpitGooBall);
                            return;
                    }

                    thinkTimer = thinkTimePerWave[Phanto.Instance.Wave];
                }
                else
                {
                    thinkTimer -= Time.fixedDeltaTime;
                }
            }

            public override void ExitState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> nextState)
            {
                b.e.ResetMovementSettings();
            }

            public override void DebugDraw(PhantoBehaviour b)
            {
                DrawStateCube(b, Color.grey);
            }

            private enum Strategy
            {
                SpitGooBall,
                Roam = SpitGooBall + 3,
                NumStrats = Roam
            }
        }

        /// <summary>
        /// The "Die" state.
        /// </summary>
        private class Die : BasePhantoState
        {
            public const float DEATH_TIME = 11f;
            public const float GROW_TIME = 6f;
            public const float MAX_GROW_SCALE = 2.3f;
            public static readonly Vector3 RCP_GROW_PERIOD = new(1f / 0.7f, 1f / 0.9f, 1f / 1.15f);
            private Vector3 initialScale;
            private Vector3 shrinkScale;

            private float time;

            public override uint GetStateID()
            {
                return (uint)StateID.Die;
            }

            public override void EnterState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> lastState)
            {
                b.e.DropSmallItems();
                b.e.DropLargeItem();

                PhantoGooSfxManager.Instance.StopMusic(true);

                initialScale = b.e.transform.localScale;

                time = 0f;
                if (b.e.isActiveAndEnabled)
                {
                    b.e.StartCoroutine(Phanto.Instance.ToggleEthereal(DEATH_TIME));
                }

                b.e.rigidbody.detectCollisions = false;

                b.e.maxAngularVelocity = 0.5f * b.e.maxAngularVelocity;

                b.targetPos = b.e.transform.position;
            }

            public override void OnCollisionStay(PhantoBehaviour b, Collision c)
            {
            }

            public override void OnProximityStay(PhantoBehaviour b, Collider c)
            {
            }

            private float CosInterp(float a, float b, float time, float rcpPeriod = 1f)
            {
                return Mathf.Lerp(a, b, 0.5f * Mathf.Cos(rcpPeriod * 2f * Mathf.PI * time) + 0.5f);
            }

            public override void UpdateState(PhantoBehaviour b)
            {
                b.e.HoverAround(b.targetPos);
                b.e.Spin(false);
                b.e.BarrelRoll(true);

                if (time < GROW_TIME)
                {
                    b.e.transform.localScale = new Vector3(
                        CosInterp(MAX_GROW_SCALE * initialScale.x, initialScale.x, time, RCP_GROW_PERIOD.x),
                        CosInterp(MAX_GROW_SCALE * initialScale.y, initialScale.y, time, RCP_GROW_PERIOD.y),
                        CosInterp(MAX_GROW_SCALE * initialScale.z, initialScale.z, time, RCP_GROW_PERIOD.z));
                    shrinkScale = b.e.transform.localScale;

                    b.e.maxAngularVelocity += 15f;
                }
                else
                {
                    b.e.transform.localScale = Vector3.Lerp(shrinkScale, Vector3.zero,
                        (time - GROW_TIME) / (DEATH_TIME - GROW_TIME));

                    b.e.maxAngularVelocity += 40f;
                }

                if (time >= DEATH_TIME)
                    b.e.DestroySelf();
                else
                    time += Time.fixedDeltaTime;
            }

            public override void ExitState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> nextState)
            {
            }

            public override void DebugDraw(PhantoBehaviour b)
            {
                DrawStateCube(b, Color.black);
            }
        }

        /// <summary>
        ///     Roams silently forever. Spooky.
        /// </summary>
        private class DemoRoam : BasePhantoState
        {
            private static readonly float[] thinkTimePerWave = { 0.6f, 0.2f, 0.083f };
            private static readonly float[] speedScalePerWave = { 0.3f, 0.5f, 0.7f };
            private static readonly float[] painTimePerWave = { 1.5f, 2f, 2.5f };

            private readonly Queue<(Vector3 cp, Vector3 dir, long ms)> contactPoints = new();
            private readonly Stopwatch contactStopwatch = Stopwatch.StartNew();
            private Vector3 dir;
            private float dirTimer;
            private float painTimer;
            private float thinkTimer;

            public override uint GetStateID()
            {
                return (uint)StateID.DemoRoam;
            }

            private void UpdateMoveSpeed(PhantoBehaviour b)
            {
                b.e.ResetMovementSettings();
                var t = Mathf.Min(1f,
                    b.e.Health / 100f + (1f - painTimer / painTimePerWave[Phanto.Instance.Wave]));
                b.e.maxVelocity *= Mathf.Lerp(1f,
                    speedScalePerWave[Phanto.Instance.Wave],
                    t);
                if (Phanto.Instance.animator.GetCurrentAnimatorClipInfo(0).Length > 0)
                    if (!Phanto.Instance.animator.GetCurrentAnimatorClipInfo(0)[0].clip.name.Equals(Phanto.HIT))
                        Phanto.Instance.animator.speed = Mathf.Min(1f / t, 4f);
            }

            public override void EnterState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> lastState)
            {
                UpdateMoveSpeed(b);

                dirTimer = Random.Range(0.067f, 0.2f);
                dir = Quaternion.AngleAxis(Random.Range(-180f, 180f) + b.e.rigidbody.rotation.eulerAngles.y,
                    Vector3.up) * Vector3.forward;

                thinkTimer = b.e.invulnerable ? 6f : thinkTimePerWave[Phanto.Instance.Wave];
            }

            public override void OnCollisionStay(PhantoBehaviour b, Collision c)
            {
                var cp = PhysicsUtils.GetAverageContact(c);

                dir = (b.e.rigidbody.position - cp).normalized;
                b.e.MoveAlong(dir);
                b.e.AimAlong(dir);

                contactPoints.Enqueue((cp, dir, contactStopwatch.ElapsedMilliseconds));
            }

            public override void OnProximityStay(PhantoBehaviour b, Collider c)
            {
                if (!PersonalBubble.IsPlayerBubble(c)) return;

                // if phanto touches the capsule floating over the player it will turn away
                var rigidbodyPosition = b.e.rigidbody.position;
                var colliderPoint = c.ClosestPoint(rigidbodyPosition);

                dir = (rigidbodyPosition - colliderPoint).normalized;
                b.e.MoveAlong(dir);
                b.e.AimAlong(dir);
            }

            public override void UpdateState(PhantoBehaviour b)
            {
                UpdateMoveSpeed(b);

                Debug.DrawRay(b.e.rigidbody.position, dir, Color.yellow);
                b.e.MoveAlong(b.e.transform.TransformDirection(Vector3.forward));
                b.e.AimAlong(dir);
                b.e.KeepUpright(Vector3.up);

                if (painTimer >= 0f) painTimer -= Time.fixedDeltaTime;

                if (dirTimer <= 0f)
                {
                    dirTimer = Random.Range(0.067f, 0.2f);
                    dir = Quaternion.AngleAxis(Random.Range(-30f, 30f) + b.e.rigidbody.rotation.eulerAngles.y,
                        Vector3.up) * Vector3.forward;
                }
                else
                {
                    dirTimer -= Time.fixedDeltaTime;
                }

                if (thinkTimer <= 0f)
                    thinkTimer = thinkTimePerWave[Phanto.Instance.Wave];
                else
                    thinkTimer -= Time.fixedDeltaTime;
            }

            public override void ExitState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> nextState)
            {
                b.e.ResetMovementSettings();
            }

            public override void DebugDraw(PhantoBehaviour b)
            {
                const long duration = 5000;
                var currentMs = contactStopwatch.ElapsedMilliseconds;

                while (contactPoints.TryPeek(out var peek))
                {
                    if (peek.ms > currentMs - duration) break;

                    contactPoints.Dequeue();
                }

                foreach (var contactPoint in contactPoints)
                {
                    XRGizmos.DrawPoint(contactPoint.cp, MSPalette.OrangeRed, 0.1f, 0.005f);
                    XRGizmos.DrawPointer(contactPoint.cp, contactPoint.dir, MSPalette.DarkCyan, 0.3f, 0.005f);
                }

                DrawStateCube(b, Color.grey);
            }

            private enum Strategy
            {
                SpitGooBall,
                Roam = SpitGooBall + 3,
                NumStrats = Roam
            }
        }

        #endregion
    }
}

/// <summary>
///     Base behaviour class for all Phanto states.
/// </summary>
public abstract class BasePhantoState : IEnemyState<PhantoBehaviour>
{
    protected static readonly Vector3 CubeSize = new(0.05f, 0.1f, 0.05f);

    public abstract uint GetStateID();
    public abstract void EnterState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> lastState);
    public abstract void OnCollisionStay(PhantoBehaviour b, Collision c);
    public abstract void OnProximityStay(PhantoBehaviour b, Collider c);
    public abstract void UpdateState(PhantoBehaviour b);
    public abstract void ExitState(PhantoBehaviour b, IEnemyState<PhantoBehaviour> nextState);

    public virtual void DebugDraw(PhantoBehaviour b)
    {
    }

    protected void DrawStateCube(PhantoBehaviour b, Color color)
    {
        XRGizmos.DrawCube(b.Position, Quaternion.identity, CubeSize, color);
    }
}
