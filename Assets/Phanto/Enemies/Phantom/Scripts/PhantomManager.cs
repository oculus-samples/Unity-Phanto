// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Phantom.Environment.Scripts;
using PhantoUtils;
using PhantoUtils.VR;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Phantom
{
    public interface IPhantomManager
    {
        public Vector3 RandomPointOnFloor(Vector3 position, float minDistance, bool verifyOpenArea = true);
        public Vector3 RandomPointOnFurniture(Vector3 position, float minDistance);
    }

    /// <summary>
    ///     Spawns and keeps track of a flock of phantoms.
    /// </summary>
    public class PhantomManager : SingletonMonoBehaviour<PhantomManager>, IPhantomManager
    {
        [Tooltip("The prefab to spawn.")]
        [SerializeField] private PhantomController phantomPrefab;

        [Tooltip("The number of phantoms to spawn.")]
        [SerializeField] private int spawnCount = 4;


        [Tooltip("The PhantomFleeTarget prefab to spawn.")]
        [SerializeField] private PhantomFleeTarget fleaTargetPrefab;


        [Tooltip("The PhantomChaseTarget prefab to spawn.")]
        [SerializeField] private PhantomChaseTarget chaseTargetPrefabPrefab;


        [Tooltip("The number of PhantomChaseTarget to spawn.")]
        [SerializeField] private int chaseInstances = 3;


        [Tooltip("The PhantomFleeTarget prefab to spawn.")]
        [SerializeField] private int fleeInstances = 8;

        [Tooltip("The maximum distance a phantom can move from the floor.")]
        [SerializeField] [Range(0.01f, 10.0f)] private float targetRefreshRate = 2.0f;

        [SerializeField] [Range(0.01f, 10.0f)] private float phantomRefreshRate = 2.0f;

        [Tooltip("Reference to Phanto")]
        [SerializeField] private Phanto.Phanto phanto;


        [Tooltip("The number of phantoms each wave")]
        [SerializeField] private int[] phantomsPerWave = { 4, 8, 16 };

        [Tooltip("The UIWaveChangeManager to handle pahntoms in waves.")]
        [SerializeField] private UIWaveChangeManager _waveChangeManager;

        private readonly HashSet<PhantomController> _activePhantoms = new();

        private readonly Queue<PhantomFleeTarget> _ouchPool = new();
        private readonly Queue<PhantomController> _phantomPool = new();
        private readonly List<PhantomController> _phantoms = new();
        private readonly List<Vector3> _spawnLocations = new();
        private readonly Queue<PhantomChaseTarget> _yumPool = new();
        private readonly HashSet<PhantomTarget> allPhantomTargets = new();

        private Bounds? _bounds;

        private NavMeshGenerator _navMeshGenerator;
        private Coroutine _phantomSpawnerCoroutine;

        private bool _started;

        private Coroutine _targetSpawnerCoroutine;

        private int _waveIndex;

        public IReadOnlyCollection<PhantomController> ActivePhantoms => _activePhantoms;
        public int MaxPhantoms => spawnCount;

        protected override void Awake()
        {
            base.Awake();

            if (spawnCount > 0)
            {
                var perWaveMax = Mathf.Max(phantomsPerWave);
                spawnCount = Mathf.Max(perWaveMax, spawnCount);
            }
            else
            {
                spawnCount = 0;
            }

            var parent = transform;

            for (var i = 0; i < chaseInstances; i++)
            {
                var yum = Instantiate(chaseTargetPrefabPrefab, parent);
                allPhantomTargets.Add(yum);
                yum.gameObject.SetSuffix($"{i:00}");
                ReturnToPool(yum);
            }

            for (var i = 0; i < fleeInstances; i++)
            {
                var ouch = Instantiate(fleaTargetPrefab, parent);
                allPhantomTargets.Add(ouch);
                ouch.gameObject.SetSuffix($"{i:00}");
                ouch.Hide();
                _ouchPool.Enqueue(ouch);
            }
        }

        private IEnumerator Start()
        {
            while (_navMeshGenerator == null)
            {
                yield return null;
                _navMeshGenerator = FindObjectOfType<NavMeshGenerator>();
            }

            for (var i = 0; i < spawnCount; i++)
            {
                var phantom = Instantiate(phantomPrefab, Vector3.zero,
                    Quaternion.AngleAxis(Random.Range(0.0f, 360.0f), Vector3.up));
                phantom.Initialize(this);
                _phantoms.Add(phantom);
                ReturnToPool(phantom);
            }

            if (phanto != null)
            {
                if (_waveChangeManager != null)
                {
                    _waveChangeManager.ManagePhantoBetweenWave(phanto.gameObject, false);
                    // delay Phanto first spawn
                    while (!_waveChangeManager.GetWaveStart()) yield return null;
                }

                while (!_bounds.HasValue) yield return null;

                var playerHeadPos = CameraRig.Instance.CenterEyeAnchor.position;
                var stopwatch = Stopwatch.StartNew();

                Vector3 spawnPoint;
                var attempts = 0;

                while (true)
                {
                    attempts++;
                    if (stopwatch.ElapsedMilliseconds > 5)
                    {
                        yield return null;
                        stopwatch.Restart();
                    }

                    var spawnBounds = _bounds.Value;
                    // shrink the bounds by a foot to keep away from walls.
                    spawnBounds.Expand(-0.3f);

                    // Spherecast downwards to make sure there's enough room for the ghost..
                    var randomPoint = spawnBounds.RandomPoint();
                    var ray = new Ray(randomPoint, Vector3.down);

                    if (!Physics.SphereCast(ray, 0.3f, out var hit, 2.0f, NavMeshConstants.SceneMeshLayerMask,
                            QueryTriggerInteraction.Ignore))
                    {
                        if (attempts <= 100)
                        {
                            continue;
                        }
                    }

                    if (hit.distance > 0.3f || attempts > 100)
                    {
                        spawnPoint = ray.GetPoint(hit.distance);

                        if (Vector3.Distance(spawnPoint, playerHeadPos) < 1.0f && attempts < 100)
                        {
                            // keep trying (within limits) if the spawn point is near users head.
                            continue;
                        }

                        break;
                    }
                }

                if (attempts > 10)
                {
                    Debug.LogWarning($"Phanto spawn placement attempts: {attempts}");
                }

                phanto.transform.position = spawnPoint;
                if (_waveChangeManager != null)
                    _waveChangeManager.ShowPhanto(true);
                else
                    phanto.gameObject.SetActive(true);
            }

            _started = true;
        }

        private void OnEnable()
        {
            _targetSpawnerCoroutine = StartCoroutine(TargetSpawner());
            _phantomSpawnerCoroutine = StartCoroutine(PhantomSpawner());

            if (phanto != null) phanto.OnWaveAdvance += OnWaveAdvance;

            SceneBoundsChecker.BoundsChanged += OnBoundsChanged;
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

            if (phanto != null) phanto.OnWaveAdvance -= OnWaveAdvance;

            SceneBoundsChecker.BoundsChanged -= OnBoundsChanged;
        }

        protected override void OnDestroy()
        {
            foreach (var target in allPhantomTargets) target.Destruct();

            allPhantomTargets.Clear();

            base.OnDestroy();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!EditorApplication.isPlaying) return;

            // Restart the targetSpawner if running, so we can adjust respawn timing live.
            if (isActiveAndEnabled && _targetSpawnerCoroutine != null)
            {
                StopCoroutine(_targetSpawnerCoroutine);
                _targetSpawnerCoroutine = StartCoroutine(TargetSpawner());
            }
        }
#endif

        public Vector3 RandomPointOnFloor(Vector3 position, float minDistance, bool verifyOpenArea = true)
        {
            return _navMeshGenerator.RandomPointOnFloor(position, minDistance, verifyOpenArea);
        }

        public Vector3 RandomPointOnFurniture(Vector3 position, float minDistance)
        {
            return FurnitureNavMeshGenerator.RandomPointOnFurniture();
        }

        private void OnBoundsChanged(Bounds bounds)
        {
            _bounds = bounds;
        }

        private void OnWaveAdvance()
        {
            Debug.Log($"[{nameof(PhantomManager)}] [{nameof(OnWaveAdvance)}]");
            _waveIndex++;
            if (_waveIndex >= phantomsPerWave.Length) _waveIndex = 0;
            if (_phantomSpawnerCoroutine == null) _phantomSpawnerCoroutine = StartCoroutine(PhantomSpawner());
            if (_waveChangeManager != null) _waveChangeManager.ManagePhantoBetweenWave(phanto.gameObject, _started);
        }

        public void ReturnToPool(PhantomChaseTarget target)
        {
            target.Hide();
            if (!target.Flee && !_yumPool.Contains(target)) _yumPool.Enqueue(target);
        }

        public void ReturnToPool(PhantomController phantom)
        {
            _activePhantoms.Remove(phantom);
            phantom.Hide();
            _phantomPool.Enqueue(phantom);
            if (_phantomSpawnerCoroutine == null) _phantomSpawnerCoroutine = StartCoroutine(PhantomSpawner());
        }

        public void SpawnOuch(Vector3 position)
        {
            foreach (var o in _ouchPool)
            {
                if (!o.Valid) continue;
                // Don't spawn an ouch inside another ouch.
                if (o.IsInBounds(position)) return;
            }

            if (!_ouchPool.TryDequeue(out var ouch))
            {
                Debug.LogWarning("ouch queue empty!");
                return;
            }

            ouch.Position = position;
            ouch.Show();

            _ouchPool.Enqueue(ouch);
        }

        private IEnumerator TargetSpawner()
        {
            var wait = new WaitForSeconds(targetRefreshRate);

            while (!_started) yield return wait;

            while (enabled)
            {
                if (_yumPool.Count > 0)
                {
                    var yum = _yumPool.Dequeue();
                    yum.Dispatch(FurnitureNavMeshGenerator.RandomPointOnFurniture());

                    yield return wait;
                }

                yield return wait;
            }
        }

        private IEnumerator PhantomSpawner()
        {
            var wait = new WaitForSeconds(phantomRefreshRate);

            while (!_started) yield return wait;

            while (_phantomPool.Count > 0 && _activePhantoms.Count < phantomsPerWave[_waveIndex])
            {
                yield return wait;

                // find a spot to spawn the phantom
                // near a goo, on the navmesh.
                var goos = PhantoGoo.ActiveGoos;
                _spawnLocations.Clear();

                foreach (var goo in goos)
                    if (NavMeshGenerator.TryGetClosestPoint(goo.Position, out var point, 0.3f))
                        _spawnLocations.Add(point);

                // either there's no furniture or floor point on goo, or queue got emptied.
                if (_spawnLocations.Count == 0 || !_phantomPool.TryDequeue(out var phantom)) continue;

                var position = _spawnLocations.RandomElement();

                phantom.Respawn(position);
                _activePhantoms.Add(phantom);
            }

            _phantomSpawnerCoroutine = null;
        }

        public void TutorialRegisterPhantoms(PhantomController phantom, bool isAdded)
        {
            if (isAdded)
                _activePhantoms.Add(phantom);
            else
                _activePhantoms.Remove(phantom);
        }
    }
}
