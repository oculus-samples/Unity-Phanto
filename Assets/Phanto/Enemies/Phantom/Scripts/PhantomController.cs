// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Diagnostics;
using Phanto.Enemies.DebugScripts;
using PhantoUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Utilities.XR;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Phantom
{
    /// <summary>
    ///     Monitors the navigation state of a phantom.
    /// </summary>
    public class PhantomController : MonoBehaviour
    {
        private const string WalkSpeedParam = "WalkSpeed";
        private const string JumpParam = "Jump";
        private const string AttackParam = "Attack";
        private const string OnGroundParam = "OnGround";

        private static readonly int WalkSpeedId = Animator.StringToHash(WalkSpeedParam);
        private static readonly int JumpParamId = Animator.StringToHash(JumpParam);
        private static readonly int AttackId = Animator.StringToHash(AttackParam);
        private static readonly int OnGroundId = Animator.StringToHash(OnGroundParam);

        private static Vector3 _gravity;

        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private Animator animator;
        [SerializeField] private new Rigidbody rigidbody;

        [SerializeField] private float turnDegreesPerSecond = 540.0f;

        [SerializeField] private Transform head;

        [SerializeField] private float fleeMultiplier = 3.0f;

        [SerializeField] [Tooltip("The jump will be this high above the highest link point")]
        private float peakHeight = 0.3f;

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

        public Vector3 Position => _transform.position;
        public Vector3 HeadPosition => head.position;

        public bool Ready { get; private set; }

        private void Awake()
        {
            if (_gravity == default) _gravity = Physics.gravity;

            _transform = transform;

            _debugOrigin = _transform.position;

            navMeshAgent.updateRotation = false;
            _defaultSpeed = navMeshAgent.speed;

            _currentTurnDPS = turnDegreesPerSecond;
            _currentSpeed = _defaultSpeed;

            navMeshAgent.autoTraverseOffMeshLink = false;

            _phantomBehaviour = GetComponent<PhantomBehaviour>();
        }

        private void Start()
        {
            Ready = true;
        }

        private void OnEnable()
        {
            DebugDrawManager.DebugDrawEvent += OnDebugDraw;
        }

        private void OnDisable()
        {
            DebugDrawManager.DebugDrawEvent -= OnDebugDraw;
        }

        public event Action DestinationReached;
        public event Action PathingFailed;

        public void Initialize(IPhantomManager manager, bool tutorial = false)
        {
            if (_phantomManager != null)
            {
                return;
            }

            _phantomManager = manager;

            gameObject.SetSuffix($"{(ushort)GetInstanceID():X4}");

            if (tutorial)
            {
                _phantomBehaviour = GetComponent<PhantomBehaviour>();
                _phantomBehaviour.Tutorial = tutorial;
            }
        }

        public void SetDestination(Vector3 destination)
        {
            if (_pathMonitorCoroutine != null)
            {
                StopCoroutine(_pathMonitorCoroutine);
                navMeshAgent.ResetPath();
            }

            _debugDestination = destination;
            _pathMonitorCoroutine = StartCoroutine(PathMonitor(destination));
        }

        public void Fleeing(bool fleeing)
        {
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
                point = _phantomManager.RandomPointOnFloor(_transform.position, 1.0f);
            else
                point = _phantomManager.RandomPointOnFurniture(_transform.position, 1.0f);

            navMeshAgent.ResetPath();
            navMeshAgent.Warp(point);

            _debugOrigin = point;
        }

        internal void SetRandomDestination()
        {
            if (_pathMonitorCoroutine != null)
            {
                StopCoroutine(_pathMonitorCoroutine);
                if (navMeshAgent.isOnNavMesh)
                {
                    navMeshAgent.ResetPath();
                }
            }

            if (_phantomManager == null || _transform == null)
            {
                Debug.LogWarning("Phantom is cleaning up or isn't fully initialized yet.");
                return;
            }

            Vector3 destination;

            if (Random.value > 0.5f)
                destination = _phantomManager.RandomPointOnFloor(_transform.position, 1.0f);
            else
                destination = _phantomManager.RandomPointOnFurniture(_transform.position, 1.0f);

            _debugDestination = destination;

            if (isActiveAndEnabled)
            {
                _pathMonitorCoroutine = StartCoroutine(PathMonitor(destination));
            }
        }

        private IEnumerator PathMonitor(Vector3 destination)
        {
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
            Debug.Log($"[{nameof(PhantomController)}] {nameof(PathMonitor)} Path calculation took: {_stopwatch.ElapsedMilliseconds}", this);
#endif

            _pathStatus = navMeshAgent.pathStatus;
            _debugStart = _transform.position;
            _debugEnd = destination;

            _pathCornerCount = navMeshAgent.path.GetCornersNonAlloc(_pathCorners);

            switch (_pathStatus.Value)
            {
                case NavMeshPathStatus.PathInvalid:
                    Debug.LogWarning(
                        $"[{nameof(PhantomController)}] {nameof(PathMonitor)} {NavMeshPathStatus.PathInvalid} ({name}) dest:{destination.ToString("F2")} target:{_phantomBehaviour.CurrentTarget?.name}",
                        this);
                    break;
                case NavMeshPathStatus.PathPartial:
                    Debug.LogWarning(
                        $"[{nameof(PhantomController)}] {nameof(PathMonitor)} {NavMeshPathStatus.PathPartial}", this);

                    NavMeshGenerator.Instance.CreateNavMeshLink(_pathCorners, _pathCornerCount, destination, 2);

                    break;
                case NavMeshPathStatus.PathComplete:
                    break;
            }

#if DEBUG && VERBOSE_DEBUG
            Debug.Log($"[{nameof(PhantomController)}] {nameof(PathMonitor)} Path corner count: {_pathCornerCount}", this);

            // Validate that last corner in path is approximately destination (destination is reachable)
            if (Vector3.Distance(_pathCorners[_pathCornerCount - 1], destination) > TennisBall)
            {
                Debug.LogWarning($"[{nameof(PhantomController)}] {nameof(PathMonitor)} End of path and destination don't match. {navMeshAgent.pathStatus}", this);
            }
#endif

            _stopwatch.Restart();
            while (navMeshAgent.remainingDistance > _phantomBehaviour.MeleeRange && navMeshAgent.hasPath)
            {
#if DEBUG && VERBOSE_DEBUG
                if (_stopwatch.ElapsedMilliseconds > 2000)
                {
                    Debug.Log($"[{nameof(PhantomController)}] {nameof(PathMonitor)} walking: {navMeshAgent.DumpState()}", this);
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

            var flatDistance = Vector3.ProjectOnPlane(destination - _transform.position, Vector3.up).magnitude;

            if (flatDistance <= _phantomBehaviour.SpitRange)
            {
                _pathStatus = null;
                for (var i = 0; i < _pathCornerCount; i++)
                {
                    _pathCorners[i] = Vector3.zero;
                }

                _pathCornerCount = 0;

                DestinationReached?.Invoke();
            }
            else
            {
                DestinationReached?.Invoke();
            }

#if DEBUG && VERBOSE_DEBUG
            Debug.Log($"[{nameof(PhantomController)}] {nameof(PathMonitor)} done: {navMeshAgent.DumpState()}", this);
#endif
            _pathMonitorCoroutine = null;
        }

        public void AttackAnimation()
        {
            animator.SetTrigger(AttackId);
        }

        public void ClearPath()
        {
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

            animator.SetFloat(WalkSpeedId, magnitude * _currentSpeed);

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

            animator.SetTrigger(JumpParamId);
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
            yield return null;
        }

        public void ReturnToPool()
        {
            gameObject.SetActive(false);
            PhantomManager.Instance.ReturnToPool(this);
        }

        public void Respawn(Vector3 position)
        {
            // move to position.
            _transform.SetPositionAndRotation(position, Quaternion.AngleAxis(Random.Range(0.0f, 360.0f), Vector3.up));
            // re-enable gameObject
            Show();
        }

        public void Show(bool visible = true)
        {
            gameObject.SetActive(visible);
        }

        public void Hide()
        {
            Show(false);
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

            if (rigidbody == null) rigidbody = GetComponent<Rigidbody>();
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
