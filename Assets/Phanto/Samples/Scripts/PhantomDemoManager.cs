// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Phanto.Enemies.DebugScripts;
using Phantom.Environment.Scripts;
using PhantoUtils;
using PhantoUtils.VR;
using UnityEngine;
using UnityEngine.AI;
using Utilities.XR;
using static NavMeshConstants;

namespace Phantom
{
    /// <summary>
    ///     Spawns and keeps track of a flock of phantoms.
    /// </summary>
    public class PhantomDemoManager : MonoBehaviour, IPhantomManager
    {
        [SerializeField] private PhantomController phantomPrefab;
        [SerializeField] private PhantomDemoChaseTarget chasePrefab;

        [SerializeField] private int spawnCount = 8;

        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        [SerializeField] protected bool debugDraw = true;

        private readonly HashSet<PhantomController> _activePhantoms = new();

        private readonly Queue<PhantomController> _phantomPool = new();
        private readonly List<PhantomController> _phantoms = new();

        private readonly Queue<(Vector3 pos, bool valid, long ms)> _pointQueue = new();
        private readonly Stopwatch _queueTimer = Stopwatch.StartNew();
        private readonly HashSet<PhantomTarget> allPhantomTargets = new();

        private PhantomDemoChaseTarget _chaseTargetInstance;

        private NavMeshGenerator _navMeshGenerator;
        private Coroutine _phantomSpawnerCoroutine;
        private bool _sceneReady;

        private bool _started;

        private Coroutine _targetSpawnerCoroutine;

        public GameplaySettings.WinCondition WinCondition => GameplaySettings.WinCondition.DefeatPhanto;

        private void Awake()
        {
            DebugDrawManager.DebugDraw = debugDraw;
        }

        private void OnEnable()
        {
            _phantomSpawnerCoroutine = StartCoroutine(PhantomSpawner());

            SceneBoundsChecker.BoundsChanged += OnBoundsChanged;
            DebugDrawManager.DebugDrawEvent += DebugDraw;
        }

        private void OnDisable()
        {
            if (_targetSpawnerCoroutine != null)
            {
                StopCoroutine(_targetSpawnerCoroutine);
                _targetSpawnerCoroutine = null;
            }

            if (_phantomSpawnerCoroutine != null)
            {
                StopCoroutine(_phantomSpawnerCoroutine);
                _phantomSpawnerCoroutine = null;
            }

            SceneBoundsChecker.BoundsChanged -= OnBoundsChanged;
            DebugDrawManager.DebugDrawEvent -= DebugDraw;
        }

        private IEnumerator Start()
        {
            _chaseTargetInstance = Instantiate(chasePrefab, transform);
            _chaseTargetInstance.Hide();

            while (_navMeshGenerator == null)
            {
                yield return null;
                _navMeshGenerator = FindObjectOfType<NavMeshGenerator>();
            }

            for (var i = 0; i < spawnCount; i++)
            {
                // spawn npc at point.
                var phantom = Instantiate(phantomPrefab, Vector3.zero,
                    Quaternion.AngleAxis(Random.Range(0.0f, 360.0f), Vector3.up));
                phantom.Initialize(this);
                _phantoms.Add(phantom);
                ReturnToPool(phantom);
            }

            _started = true;
        }

        protected void OnDestroy()
        {
            foreach (var target in allPhantomTargets) target.Destruct();

            allPhantomTargets.Clear();
        }

        private void Update()
        {
            XRGizmos.DrawPointer(leftHand.position, leftHand.forward, Color.blue, 0.5f);
            XRGizmos.DrawPointer(rightHand.position, rightHand.forward, Color.red, 0.5f);

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
            {
                var ray = new Ray(leftHand.position, leftHand.forward);

                SpawnTarget(ray);
            }
            else if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                var ray = new Ray(rightHand.position, rightHand.forward);

                SpawnTarget(ray);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="ray"></param>
        private void SpawnTarget(Ray ray)
        {
            // shoot a ray at the scene mesh
            // if the point is near navmesh spawn a chase target.

            if (Physics.SphereCast(ray, TennisBall, out var sphereHit, 100.0f, SceneMeshLayerMask))
            {
                var point = sphereHit.point;
                var validPoint = false;

                // check if the point is near navmesh.
                if (NavMesh.SamplePosition(point, out var navMeshHit, 0.05f, NavMesh.AllAreas))
                {
                    point = navMeshHit.position;
                    validPoint = true;

                    _chaseTargetInstance.Position = point;
                    _chaseTargetInstance.Show();

                    foreach (var phantom in _activePhantoms) phantom.SetDestination(_chaseTargetInstance, point);
                }

                _pointQueue.Enqueue((point, validPoint, _queueTimer.ElapsedMilliseconds));
            }
        }

        private void OnBoundsChanged(Bounds bounds)
        {
            _sceneReady = true;
        }

        public void CreateNavMeshLink(Vector3[] pathCorners, int pathCornerCount, Vector3 destination, int areaId)
        {
            _navMeshGenerator.CreateNavMeshLink(pathCorners, pathCornerCount, destination, areaId);
        }

        public void ReturnToPool(PhantomController phantom)
        {
            _activePhantoms.Remove(phantom);

            phantom.Hide();
            _phantomPool.Enqueue(phantom);

            if (_phantomSpawnerCoroutine == null) _phantomSpawnerCoroutine = StartCoroutine(PhantomSpawner());
        }

        private IEnumerator PhantomSpawner()
        {
            var wait = new WaitForSeconds(0.1f);

            var head = CameraRig.Instance.CenterEyeAnchor;

            while (!_started || !_sceneReady) yield return wait;

            while (_phantomPool.Count > 0)
            {
                yield return wait;

                var headPosition = head.position;

                if (!_phantomPool.TryDequeue(out var phantom)) continue;

                var position = Random.value > 0.5f
                    ? SceneQuery.RandomPointOnFloor(headPosition, 0.5f)
                    : SceneQuery.RandomPointOnFurniture(headPosition, 0.5f);

                phantom.Respawn(position);
                _activePhantoms.Add(phantom);
            }

            _phantomSpawnerCoroutine = null;
        }

        public void SpawnOuch(Vector3 position, Vector3 normal)
        {
        }

        public void DecrementPhantom()
        {
        }

        private void DebugDraw()
        {
            const long duration = 2000;
            var currentMs = _queueTimer.ElapsedMilliseconds;

            while (_pointQueue.TryPeek(out var peek))
            {
                if (peek.ms > currentMs - duration) break;

                _pointQueue.Dequeue();
            }

            foreach (var p in _pointQueue)
                XRGizmos.DrawPoint(p.pos, p.valid ? MSPalette.Green : MSPalette.Red, 0.2f, 0.005f);
        }
    }
}
