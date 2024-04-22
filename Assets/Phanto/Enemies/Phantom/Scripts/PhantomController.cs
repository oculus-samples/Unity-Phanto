// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Diagnostics;
using Phanto.Audio.Scripts;
using Phanto.Enemies.DebugScripts;
using PhantoUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using Utilities.XR;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Phantom
{
    /// <summary>
    ///     Monitors the navigation state of a phantom.
    /// </summary>
    [SelectionBase]
    public class PhantomController : MonoBehaviour
    {
        private const string WalkSpeedParam = "WalkSpeed";
        private const string JumpParam = "Jump";
        private const string AttackParam = "Attack";
        private const string OnGroundParam = "OnGround";

        private static readonly int WalkSpeedId = Animator.StringToHash(WalkSpeedParam);
        private static readonly int JumpId = Animator.StringToHash(JumpParam);
        private static readonly int AttackId = Animator.StringToHash(AttackParam);
        private static readonly int OnGroundId = Animator.StringToHash(OnGroundParam);

        // Emotes
        private const string AlertParam = "Alert";
        private const string AngryParam = "Angry";
        private const string ExclamationParam = "Exclamation";
        private const string GhostlyParam = "Ghostly";
        private const string SurpriseParam = "Surprise";

        private static readonly int AlertId = Animator.StringToHash(AlertParam);
        private static readonly int AngryId = Animator.StringToHash(AngryParam);
        private static readonly int ExclamationId = Animator.StringToHash(ExclamationParam);
        private static readonly int GhostlyId = Animator.StringToHash(GhostlyParam);
        private static readonly int SurpriseId = Animator.StringToHash(SurpriseParam);

        private static Vector3 _gravity;

        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private Animator animator;

        [SerializeField] private float turnDegreesPerSecond = 540.0f;

        [SerializeField] private Transform head;

        [SerializeField] [Tooltip("The jump will be this high above the highest link point")]
        private float peakHeight = 0.3f;

        [SerializeField] private float animationMultiplier = 1.0f;

        private readonly Vector3[] _pathCorners = new Vector3[256];

        private readonly Stopwatch _stopwatch = new();
        private float _currentSpeed;
        private float _currentTurnDPS;
        private Vector3? _debugDestination;
        private Vector3 _debugEnd;

        private Vector3? _debugOrigin;

        private Vector3 _debugStart;
        private float _defaultSpeed;
        private int _pathCornerCount;
        private Coroutine _pathMonitorCoroutine;
        private NavMeshPathStatus? _pathStatus;

        private PhantomBehaviour _phantomBehaviour;

        private IPhantomManager _phantomManager;
        private Transform _transform;
        private OriginCrystal _originCrystal;

        private GameplaySettingsManager _gameplaySettingsManager;
        private bool _gameplaySettingsReady;

        private PhantoGooSfxManager _phantoGooSfxManager;
        private bool _phantoGooSfxReady;

        public Vector3 Position => _transform.position;
        public Vector3 HeadPosition => head.position;

        public GameplaySettings.WinCondition WinCondition => _phantomManager.WinCondition;
        public bool Ready { get; private set; }

        public event Action<bool> DestinationReached;
        public event Action PathingFailed;

        private void Awake()
        {
            if (_gravity == default) _gravity = Physics.gravity;

            _transform = transform;

            _debugOrigin = _transform.position;

            navMeshAgent.updateRotation = false;

            _currentTurnDPS = turnDegreesPerSecond;

            navMeshAgent.autoTraverseOffMeshLink = false;

            _phantomBehaviour = GetComponent<PhantomBehaviour>();
        }

        private void Start()
        {
            Ready = true;
        }

        private void OnEnable()
        {
            StartCoroutine(ApplySettings());
            DebugDrawManager.DebugDrawEvent += OnDebugDraw;
        }

        private IEnumerator ApplySettings()
        {
            if (_phantomManager is TutorialPhantomManager)
            {
                yield break;
            }

            yield return new WaitUntil(()=>_gameplaySettingsReady);
            // Set phantom speed depending on settings
            _defaultSpeed = _gameplaySettingsManager.gameplaySettings.CurrentPhantomSetting.Speed;
            _currentSpeed = _defaultSpeed;
            navMeshAgent.speed = _currentSpeed;
        }

        private void OnDisable()
        {
            DebugDrawManager.DebugDrawEvent -= OnDebugDraw;
        }

        public void Initialize(IPhantomManager manager, bool tutorial = false)
        {
            if (_phantomManager != null)
            {
                Debug.LogWarning($"{name} got Initialized twice.", this);
                return;
            }

            _phantomManager = manager;

            _gameplaySettingsManager = GameplaySettingsManager.Instance;
            _gameplaySettingsReady = _gameplaySettingsManager != null;

            _phantoGooSfxManager = PhantoGooSfxManager.Instance;
            _phantoGooSfxReady = _phantoGooSfxManager != null;

            if (!_gameplaySettingsReady || !_phantoGooSfxReady)
            {
                Debug.LogWarning($"Managers available: GameplaySettingsManager: {_gameplaySettingsReady} SfxManager: {_phantoGooSfxReady}", this);
            }

            gameObject.SetSuffix($"{(ushort)GetInstanceID():X4}");

            if (tutorial)
            {
                _phantomBehaviour = GetComponent<PhantomBehaviour>();
                _phantomBehaviour.Tutorial = tutorial;
            }
        }

        public void SetDestination(PhantomTarget target, Vector3 destination)
        {
            ClearPath();

            _debugDestination = destination;
            _pathMonitorCoroutine = StartCoroutine(PathMonitor(target, destination));
        }

        public void SetCrystalTarget(PhantomTarget target)
        {
            _phantomBehaviour.SetCrystalTarget(target);
        }

        public void Fleeing(bool fleeing)
        {
            float fleeMultiplier;

            if (_phantomManager is TutorialPhantomManager)
            {
                fleeMultiplier = 1.5f;
            }
            else
            {
                fleeMultiplier = _gameplaySettingsManager.gameplaySettings.CurrentPhantomSetting
                    .FleeSpeedMultiplier;
            }

            _currentSpeed = fleeing ? _defaultSpeed * fleeMultiplier : _defaultSpeed;
            _currentTurnDPS = fleeing ? turnDegreesPerSecond * fleeMultiplier : turnDegreesPerSecond;
        }

        private void PickNewDestination()
        {
            IEnumerator WaitAFew()
            {
                yield return new WaitForSeconds(1.0f);
                SetRandomDestination();
            }

            StartCoroutine(WaitAFew());
        }

        internal void Teleport()
        {
            Vector3 point;

            if (Random.value > 0.5f)
                point = SceneQuery.RandomPointOnFloor(_transform.position, 1.0f);
            else
                point = SceneQuery.RandomPointOnFurniture(_transform.position, 1.0f);

            navMeshAgent.ResetPath();
            navMeshAgent.Warp(point);

            _debugOrigin = point;
        }

        internal void SetRandomDestination()
        {
            if (_phantomManager == null || _transform == null)
            {
                Debug.LogWarning("Phantom is cleaning up or isn't fully initialized yet.");
                return;
            }

            Vector3 destination;

            if (Random.value > 0.5f)
                destination = SceneQuery.RandomPointOnFloor(_transform.position, 1.0f);
            else
                destination = SceneQuery.RandomPointOnFurniture(_transform.position, 1.0f);

            if (isActiveAndEnabled)
            {
                SetDestination(null, destination);
            }
        }

        private IEnumerator PathMonitor(PhantomTarget target, Vector3 destination)
        {
            // Destination point is not NaN.
            Assert.IsTrue(destination.IsSafeValue());

            // Unity's NavMeshAgent doesn't have any public events, and has limited public state,
            // so you have to constantly poll the state of the agent to see if anything important has changed.
            _stopwatch.Restart();

            // SetDestination starts background path calculation.
            var success = navMeshAgent.SetDestination(destination);

            if (!success)
            {
                PathingFailed?.Invoke();
#if DEBUG
                Debug.LogWarning(
                    $"[{nameof(PhantomController)}] {nameof(PathMonitor)} CalculatePath failed? {destination.ToString("F2")} {_phantomBehaviour.CurrentTarget.name} [{name}]",
                    this);
                Debug.Break();
#endif
            }

            do
            {
                yield return null;
            } while (navMeshAgent.pathPending);

#if DEBUG && VERBOSE_DEBUG
            Debug.Log(
                $"[{nameof(PhantomController)}] {nameof(PathMonitor)} Path calculation took: {_stopwatch.ElapsedMilliseconds}",
                this);
#endif

            _pathStatus = navMeshAgent.pathStatus;
            _debugStart = _transform.position;
            _debugEnd = destination;

            _pathCornerCount = navMeshAgent.path.GetCornersNonAlloc(_pathCorners);

            switch (_pathStatus.Value)
            {
                case NavMeshPathStatus.PathInvalid:
#if UNITY_EDITOR
                    Debug.LogWarning(
                        $"[{nameof(PhantomController)}] {nameof(PathMonitor)} {NavMeshPathStatus.PathInvalid} ({name}) dest:{destination.ToString("F2")} pos: {Position.ToString("F2")} target:{_phantomBehaviour.CurrentTarget?.name}\nagentState: {navMeshAgent.DumpState()}",
                        this);
#else
                    Debug.LogWarning(
                        $"[{nameof(PhantomController)}] {nameof(PathMonitor)} {NavMeshPathStatus.PathInvalid} ({name}) dest:{destination.ToString("F2")} pos: {Position.ToString("F2")} target:{_phantomBehaviour.CurrentTarget?.name}",
                        this);
#endif

                    // Can't currently reach the actual destination (congested?), so attempt to reach a point on the way to the destination.
                    var newDestination = Vector3.Lerp(Position, destination, Random.Range(0.6f, 0.9f));
                    if (NavMesh.SamplePosition(newDestination, out var hit, 10.0f, NavMesh.AllAreas))
                    {
                        SetDestination(target, hit.position);
                        yield break;
                    }

                    break;
                case NavMeshPathStatus.PathPartial:
                    Debug.LogWarning(
                        $"[{nameof(PhantomController)}] {nameof(PathMonitor)} {NavMeshPathStatus.PathPartial}", this);

                    _phantomManager.CreateNavMeshLink(_pathCorners, _pathCornerCount, destination, NavMeshConstants.JumpArea);

                    break;
                case NavMeshPathStatus.PathComplete:
                    break;
            }

#if DEBUG && VERBOSE_DEBUG
            Debug.Log($"[{nameof(PhantomController)}] {nameof(PathMonitor)} Path corner count: {_pathCornerCount}",
                this);

            if (_pathCornerCount > 0)
            {
                var debugDistance2d =
 Vector3.ProjectOnPlane(_pathCorners[_pathCornerCount - 1] - destination, Vector3.up)
                    .magnitude;

                // Validate that last corner in path is approximately destination (destination is reachable)
                if (debugDistance2d > NavMeshConstants.OneFoot)
                {
                    Debug.LogWarning(
                        $"[{nameof(PhantomController)}] {nameof(PathMonitor)} End of path and destination don't match. status: {navMeshAgent.pathStatus} hasPath: {navMeshAgent.hasPath} corners: {_pathCornerCount}",
                        this);
                }
            }
#endif

            _stopwatch.Restart();
            var repathCheck = 0.0f;

            while (navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
            {
                if (!navMeshAgent.hasPath)
                {
                    break;
                }

                repathCheck += Time.deltaTime;

                if (repathCheck > 0.5f)
                {
                    repathCheck = 0.0f;
                    if (navMeshAgent.isPathStale)
                    {
#if DEBUG && VERBOSE_DEBUG
#if UNITY_EDITOR
                        Debug.Log(
                            $"[{nameof(PhantomController)}] {nameof(PathMonitor)} path is stale, recalculating: {navMeshAgent.DumpState()}",
                            this);
#else
                        Debug.Log(
                            $"[{nameof(PhantomController)}] {nameof(PathMonitor)} path is stale, recalculating",
                            this);
#endif
#endif
                        // The navmesh has changed (links added, hole poked), so recalculate your path to destination.
                        SetDestination(target, destination);
                        yield break;
                    }
                }

#if DEBUG && VERBOSE_DEBUG
                if (_stopwatch.ElapsedMilliseconds > 2000)
                {
#if UNITY_EDITOR
                    Debug.Log(
                        $"[{nameof(PhantomController)}] {nameof(PathMonitor)} walking: {navMeshAgent.DumpState()}",
                        this);
#else
                    Debug.Log(
                        $"[{nameof(PhantomController)}] {nameof(PathMonitor)} walking {Position.ToString("F2")}",
                        this);
#endif
                    _stopwatch.Restart();
                }
#endif

                if (navMeshAgent.isOnOffMeshLink)
                {
                    var link = navMeshAgent.currentOffMeshLinkData;
                    yield return StartCoroutine(CrossGap(link));
                }

                Move(navMeshAgent.desiredVelocity);
                yield return null;
            }

#if DEBUG && VERBOSE_DEBUG
            // Make sure agent has come to a stop.
            if (!navMeshAgent.hasPath || Mathf.Approximately(navMeshAgent.velocity.sqrMagnitude, 0f))
            {
                Debug.Log($"[{nameof(PhantomController)}] {nameof(PathMonitor)} stopped.", this);
            }
#endif

            // Stop movement & transition to idle animation.
            Move(Vector3.zero);

            Vector3 actionPoint = destination;
            if (target != null)
            {
                switch (target)
                {
                    case PhantomChaseTarget _:
                    case RangedFurnitureTarget _:
                        actionPoint = target.GetAttackPoint();
                        break;
                    case PhantomFleeTarget _:
                        break;
                }
            }

            var flatDistance = Vector3.ProjectOnPlane(actionPoint - Position, Vector3.up).magnitude;

            if (flatDistance <= _phantomBehaviour.SpitRange)
            {
                _pathStatus = null;
                for (var i = 0; i < _pathCornerCount; i++)
                {
                    _pathCorners[i] = Vector3.zero;
                }

                _pathCornerCount = 0;

                DestinationReached?.Invoke(true);
            }
            else
            {
                // path finding completed or failed, but agent not within attack range.
                DestinationReached?.Invoke(false);
            }

#if DEBUG && VERBOSE_DEBUG
#if UNITY_EDITOR
            Debug.Log($"[{nameof(PhantomController)}] {nameof(PathMonitor)} done: {navMeshAgent.DumpState()}", this);
#else
            Debug.Log($"[{nameof(PhantomController)}] {nameof(PathMonitor)} done: {Position.ToString("F2")}", this);
#endif
#endif
            _pathMonitorCoroutine = null;
        }

        public void AttackAnimation()
        {
            animator.SetTrigger(AttackId);
        }

        public void PlayEmote(Thought thought)
        {
            int animId;

            switch (thought)
            {
                case Thought.Surprise: // Enters flee state.
                    animId = SurpriseId;
                    break;
                case Thought.Angry: // Enter attack state.
                    animId = AngryId;
                    break;
                case Thought.Exclamation: // Enter chase state.
                    animId = ExclamationId;
                    break;
                case Thought.Alert: // Enter roam state
                    animId = AlertId;
                    break;
                case Thought.Ghost: // TODO: Victory state.
                    animId = GhostlyId;
                    break;
                default:
                    return;
            }

            animator.SetTrigger(animId);
        }

        public void ClearPath()
        {
#if DEBUG && VERBOSE_DEBUG
            Debug.Log("stopping previous pathing...", this);
#endif

            if (_pathMonitorCoroutine != null)
            {
                StopCoroutine(_pathMonitorCoroutine);
                _pathMonitorCoroutine = null;
            }

            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.ResetPath();
            }

            Move(Vector3.zero);
        }

        /// <summary>
        ///     Pass desired velocity info to the animator component
        ///     move the agent forward a bit
        /// </summary>
        /// <param name="desiredVelocity"></param>
        private void Move(Vector3 desiredVelocity)
        {
            var deltaTime = Time.deltaTime;

            var magnitude = desiredVelocity.magnitude;
            var walkSpeed = magnitude * _currentSpeed * animationMultiplier;

            // Set the leg's layer weight based on if we are animating legs or not.
            animator.SetLayerWeight(1, walkSpeed > 0.05f ? 0.5f : 0.0f);
            animator.SetFloat(WalkSpeedId, walkSpeed);

            if (magnitude == 0) return;

            // turn toward the desired velocity.
            var rotation = Quaternion.RotateTowards(_transform.rotation,
                Quaternion.LookRotation(desiredVelocity, Vector3.up), _currentTurnDPS * deltaTime);

            _transform.rotation = rotation;
        }

        private IEnumerator CrossGap(OffMeshLinkData offMeshLinkData)
        {
            var start = offMeshLinkData.startPos;
            var end = offMeshLinkData.endPos;

            var elapsed = 0.0f;

            // can't die while mid hop.
            _phantomBehaviour.SetOnGround(false);

            animator.SetTrigger(JumpId);
            animator.SetBool(OnGroundId, false);

            var (velocity, duration) = CalculateHopVelocity(start, end, peakHeight, _gravity.y);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var position = GetPosition(start, velocity, _gravity, elapsed);

                transform.position = position;
                yield return null;
            }

            // Make sure the end point of the hop is on the navmesh.
            if (NavMesh.SamplePosition(end, out var navMeshHit, 1.0f, NavMesh.AllAreas))
            {
                end = navMeshHit.position;
            }

            transform.position = end;
            // this *should* place the agent at the end point of the OffMeshLink
            // but if the end point is under the navmesh a teleport to 0,0,0 will happen.
            navMeshAgent.CompleteOffMeshLink();
            // warp to make sure the navMeshAgent position agrees with the transform's position.
            navMeshAgent.Warp(end);

            animator.SetBool(OnGroundId, true);
            _phantomBehaviour.SetOnGround(true);
            if (_phantoGooSfxReady)
            {
                _phantoGooSfxManager.PlayPhantomHopSfx(end);
            }

            yield return null;
        }

        public void ReturnToPool()
        {
            if (_originCrystal != null)
            {
                _originCrystal.UnregisterPhantom(this);
            }

            gameObject.SetActive(false);
            _phantomManager.ReturnToPool(this);
        }

        public void Respawn(Vector3 position)
        {
            // snap position to NavMesh.
            if (NavMesh.SamplePosition(position, out var navMeshHit, NavMeshConstants.OneFoot, NavMesh.AllAreas))
            {
                position = navMeshHit.position;
            }

            // move to position.
            _transform.SetPositionAndRotation(position, Quaternion.AngleAxis(Random.Range(0.0f, 360.0f), Vector3.up));
            // re-enable gameObject
            Show();
        }

        public void SetOriginCrystal(OriginCrystal crystal)
        {
            _originCrystal = crystal;
            _originCrystal.RegisterPhantom(this);
        }

        public void RequestCrystalTarget()
        {
            if (_originCrystal == null)
            {
                _originCrystal = OriginCrystal.GetClosestOrigin(Position);
                Assert.IsNotNull(_originCrystal);
                _originCrystal.RegisterPhantom(this);
            }

            _originCrystal.SelectSquadTarget();
        }

        public void SpawnOuch(Vector3 position, Vector3 normal)
        {
            _phantomManager.SpawnOuch(position, normal);
        }

        public void DecrementPhantom()
        {
            _phantomManager.DecrementPhantom();
        }

        public void Show(bool visible = true)
        {
            gameObject.SetActive(visible);
        }

        public void Hide()
        {
            Show(false);
        }

        public void ResetState()
        {
            if (_originCrystal != null)
            {
                _originCrystal.UnregisterPhantom(this);
            }

            _originCrystal = null;
            _phantomBehaviour.ResetState();
        }

        internal static (Vector3, float) CalculateHopVelocity(Vector3 start, Vector3 end, float peakHeight,
            float gravity)
        {
            var maxY = Mathf.Max(start.y, end.y);
            var maxHeight = maxY + peakHeight;
            var heightAboveEnd = maxHeight - end.y;
            var heightAboveStart = maxHeight - start.y;

            // calculate total flight time of the projectile.
            // time from start to max height + time from max height to end height
            var flightTime = Mathf.Sqrt(-2 * heightAboveStart / gravity) + Mathf.Sqrt(-2 * heightAboveEnd / gravity);

            var velocity = (end - start) / flightTime;
            velocity.y -= gravity * flightTime / 2.0f; // add vertical velocity to counteract gravity.

            return (velocity, flightTime);
        }

        private static Vector3 GetPosition(Vector3 startPosition, Vector3 velocity, Vector3 gravity, float time)
        {
            return startPosition + velocity * time + 0.5f * time * time * gravity;
        }

        private void OnDebugDraw()
        {
            if (_pathStatus.HasValue)
            {
                Color color = default;

                switch (_pathStatus.Value)
                {
                    case NavMeshPathStatus.PathComplete:
                        color = MSPalette.Green;
                        break;
                    case NavMeshPathStatus.PathInvalid:
                        color = MSPalette.Crimson;
                        break;
                    case NavMeshPathStatus.PathPartial:
                        color = MSPalette.DeepSkyBlue;
                        break;
                }

                var start = _debugStart;
                var end = _debugEnd;

                XRGizmos.DrawPoint(start, MSPalette.OrangeRed, 0.15f, 0.006f);
                XRGizmos.DrawPoint(end, MSPalette.OrangeRed, 0.15f, 0.006f);

                if (_pathCornerCount > 0)
                {
                    XRGizmos.DrawLineList(_pathCorners, color, false, _pathCornerCount, 0.006f);
                    XRGizmos.DrawPointSet(_pathCorners, color, 0.1f, _pathCornerCount, 0.006f);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_debugDestination.HasValue)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(_debugDestination.Value, 0.1f);
            }

            if (_debugOrigin.HasValue)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(_debugOrigin.Value, 0.05f);
            }

            if (_pathCornerCount != 0)
            {
                Gizmos.color = Color.blue;
                for (var i = 0; i < _pathCornerCount; i++)
                {
                    Gizmos.DrawSphere(_pathCorners[i], 0.05f);
                    if (i > 0) Gizmos.DrawLine(_pathCorners[i - 1], _pathCorners[i]);
                }
            }
        }

        private void OnValidate()
        {
            if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();

            if (animator == null) animator = GetComponentInChildren<Animator>();
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(PhantomController))]
    public class PhantomControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (EditorApplication.isPlaying)
            {
                GUILayout.Space(16);
                if (GUILayout.Button("Teleport"))
                {
                    var phantom = target as PhantomController;
                    phantom.Teleport();
                }

                if (GUILayout.Button("Set Random Destination"))
                {
                    var phantom = target as PhantomController;
                    phantom.SetRandomDestination();
                }
            }
        }
    }
#endif
}
