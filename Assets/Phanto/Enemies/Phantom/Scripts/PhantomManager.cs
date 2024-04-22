// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Phantom.Environment.Scripts;
using PhantoUtils;
using PhantoUtils.VR;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using Classification = OVRSceneManager.Classification;
using static NavMeshConstants;

namespace Phantom
{
    /// <summary>
    ///     Spawns and keeps track of a flock of phantoms.
    /// </summary>
    public class PhantomManager : SingletonMonoBehaviour<PhantomManager>, IPhantomManager
    {
        // Objects that should be appropriate for placing a crystal on.
        public static readonly string[] CrystalSurfaces = new[]
            { Classification.Table, Classification.Couch, Classification.Bed, Classification.Storage, Classification.Other };

        [Tooltip("The prefab to spawn.")] [SerializeField]
        private PhantomController phantomPrefab;

        [Tooltip("The PhantomFleeTarget prefab to spawn.")] [SerializeField]
        protected PhantomFleeTarget fleaTargetPrefab;

        [Tooltip("The PhantomChaseTarget prefab to spawn.")] [SerializeField]
        private PhantomChaseTarget chaseTargetPrefabPrefab;

        [Tooltip("The number of PhantomChaseTarget to spawn.")] [SerializeField]
        private int chaseInstances = 3;

        [Tooltip("The PhantomFleeTarget prefab to spawn.")] [SerializeField]
        protected int fleeInstances = 8;

        [Tooltip("The maximum distance a phantom can move from the floor.")] [SerializeField] [Range(0.01f, 10.0f)]
        private float targetRefreshRate = 2.0f;

        [Tooltip("Reference to Phanto")] [SerializeField]
        protected Phanto.Phanto phanto;

        [Tooltip("The UIWaveChangeManager to handle pahntoms in waves.")] [SerializeField]
        private UIWaveChangeManager _waveChangeManager;

        [SerializeField] private TableCrystal tableCrystalPrefab;

        [SerializeField] private OriginCrystal originCrystalPrefab;

        [SerializeField] protected GameplaySettingsManager settingsManager;

        protected readonly HashSet<PhantomController> _activePhantoms = new();

        protected readonly Queue<PhantomFleeTarget> _ouchPool = new();
        private readonly Queue<PhantomController> _phantomPool = new();
        private readonly List<PhantomController> _phantoms = new();
        private readonly List<Vector3> _spawnLocations = new();
        private readonly Queue<PhantomChaseTarget> _yumPool = new();
        protected readonly HashSet<PhantomTarget> _allPhantomTargets = new();

        private Bounds? _bounds;

        private NavMeshGenerator _navMeshGenerator;
        protected Coroutine _phantomSpawnerCoroutine;
        protected int _spawnCount = 4;

        private bool _started;

        private Coroutine _extinguishGooCoroutine;
        private Coroutine _targetSpawnerCoroutine;
        protected OVRSceneRoom _sceneRoom = null;

        private int _phantomsDestroyedCount;
        protected int _phantomWaveCount = 0;
        protected TableCrystal _tableCrystalInstance;
        protected WaitForSeconds _spawnWait = new WaitForSeconds(1);

        protected readonly Queue<OriginCrystal> _originCrystals = new Queue<OriginCrystal>();

        public IReadOnlyCollection<PhantomController> ActivePhantoms => _activePhantoms;
        public int MaxPhantoms => _spawnCount;

        public GameplaySettings.WinCondition WinCondition { get; protected set; } =
            GameplaySettings.WinCondition.DefeatPhanto;

        protected override void Awake()
        {
            base.Awake();

            Assert.IsNotNull(settingsManager);
        }

        protected virtual void Init()
        {
            if (_spawnCount > 0)
            {
                var perWaveMax = GameplaySettingsManager.Instance.gameplaySettings.PhantomSettingsList.Select(
                    (phantomsSetting) => phantomsSetting.Quantity
                ).ToList().Max();
                _spawnCount = Mathf.Max(perWaveMax, _spawnCount);
            }
            else
            {
                _spawnCount = 0;
            }

            var parent = transform;

            for (var i = 0; i < chaseInstances; i++)
            {
                var yum = Instantiate(chaseTargetPrefabPrefab, parent);
                _allPhantomTargets.Add(yum);
                yum.gameObject.SetSuffix($"{i:00}");
                ReturnToPool(yum);
            }

            for (var i = 0; i < fleeInstances; i++)
            {
                var ouch = Instantiate(fleaTargetPrefab, parent);
                _allPhantomTargets.Add(ouch);
                ouch.gameObject.SetSuffix($"{i:00}");
                ouch.Hide();
                _ouchPool.Enqueue(ouch);
            }
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => GameplaySettingsManager.Instance != null);
            Init();

            if (phanto != null)
            {
                phanto.gameObject.SetActive(false);
            }

            while (_navMeshGenerator == null)
            {
                yield return null;
                _navMeshGenerator = FindObjectOfType<NavMeshGenerator>();
            }

            for (var i = 0; i < _spawnCount; i++)
            {
                var phantom = Instantiate(phantomPrefab, Vector3.zero,
                    Quaternion.AngleAxis(Random.Range(0.0f, 360.0f), Vector3.up));
                phantom.Initialize(this);
                _phantoms.Add(phantom);
                ReturnToPool(phantom);
            }

            while (!_bounds.HasValue || _sceneRoom == null) yield return null;

            _started = true;
            OnWaveAdvance();
        }

        protected virtual void OnEnable()
        {
            _targetSpawnerCoroutine = StartCoroutine(TargetSpawner());
            _phantomSpawnerCoroutine = StartCoroutine(PhantomSpawner());

            settingsManager.OnWaveAdvance += OnWaveAdvance;

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

            settingsManager.OnWaveAdvance -= OnWaveAdvance;

            SceneBoundsChecker.BoundsChanged -= OnBoundsChanged;
        }

        protected override void OnDestroy()
        {
            foreach (var target in _allPhantomTargets) target.Destruct();

            _allPhantomTargets.Clear();

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

        [ContextMenu("TestAdvanceWave")]
        public void TestAdvanceWave()
        {
            settingsManager.AdvanceWave();
        }
#endif

        public void EndCrystalWave()
        {
            if (WinCondition == GameplaySettings.WinCondition.DefeatAllPhantoms)
            {
                RoundOver();
            }
        }

        public virtual void DecrementPhantom()
        {
            switch (WinCondition)
            {
                case GameplaySettings.WinCondition.DefeatPhanto:
                    // shouldn't care about phantom elimination
                    // care about statistics?
                    break;
                case GameplaySettings.WinCondition.DefeatAllPhantoms:
                    // TODO: adjust the phantom rounds win conditions
                    if (--_phantomWaveCount <= 0)
                    {
                        RoundOver();
                    }

                    break;
            }
        }

        protected virtual void RoundOver(bool victory = true)
        {
            // victory. round over.
            settingsManager.AdvanceWave();
        }

        public void OnSceneDataProcessed(Transform sceneRoot)
        {
            _sceneRoom = sceneRoot.GetComponentInChildren<OVRSceneRoom>();

            Assert.IsNotNull(_sceneRoom);
        }

        protected void OnBoundsChanged(Bounds bounds)
        {
            _bounds = bounds;
        }

        /// <summary>
        /// Start of a new wave (start spawning phantoms for wave).
        /// Event sent from UIWaveChangeManager
        /// </summary>
        public void OnNewWave()
        {
            Debug.Log($"[{nameof(PhantomManager)}] [{nameof(OnNewWave)}]");
            if (_phantomSpawnerCoroutine == null) _phantomSpawnerCoroutine = StartCoroutine(PhantomSpawner());
        }

        /// <summary>
        /// Advancing to next wave, but hasn't shown wave UI yet.
        /// </summary>
        protected virtual void OnWaveAdvance()
        {
            Debug.Log($"[{nameof(PhantomManager)}] [{nameof(OnWaveAdvance)}]");

            if (_waveChangeManager != null) _waveChangeManager.ManagePhantoBetweenWave(phanto.gameObject, _started);
            _phantomsDestroyedCount = 0;

            // Figure out what the wave's Phantom behavior is supposed to be.

            var currentWave = GameplaySettingsManager.Instance.gameplaySettings.CurrentWave;
            var phantomSettings = currentWave.phantomSetting;

            Debug.Log($"wave type: {currentWave.winCondition} PhantomSettings: {phantomSettings}");

            WinCondition = currentWave.winCondition;
            _phantomWaveCount = currentWave.phantomSetting.Quantity;
            _spawnWait = new WaitForSeconds(currentWave.phantomSetting.SpawnRate);

            if (_phantomSpawnerCoroutine != null)
            {
                StopCoroutine(_phantomSpawnerCoroutine);
                _phantomSpawnerCoroutine = null;
            }

            if (_targetSpawnerCoroutine != null)
            {
                StopCoroutine(_targetSpawnerCoroutine);
                _targetSpawnerCoroutine = null;
            }

            if (_tableCrystalInstance != null)
            {
                _tableCrystalInstance.Destruct();
            }

            DestroyOriginCrystals();

            // return all targets currently in the room
            foreach (var target in _allPhantomTargets)
            {
                target.Hide();
            }

            switch (currentWave.winCondition)
            {
                case GameplaySettings.WinCondition.DefeatPhanto:
                    var playerHeadPos = CameraRig.Instance.CenterEyeAnchor.position;

                    var spawnPoint = FindPhantoSpawnPosition(playerHeadPos);

                    phanto.transform.position = spawnPoint;

                    // delete any crystals
                    DeactivateCrystals();

                    // resume the chase target spawner
                    _targetSpawnerCoroutine = StartCoroutine(TargetSpawner());

                    // phantoms just wander around until they detect a target.
                    break;
                case GameplaySettings.WinCondition.DefeatAllPhantoms:
                    RemoveGoo();

                    // spawn window/door crystals.
                    // spawn a table crystal
                    ActivateCrystals();

                    // Create a spawn point that can't be removed for the phantoms.
                    CreateSpawnPoints(currentWave.phantomSetting.SpawnPoints);
                    break;
            }

            // any existing phantoms need to have their attack delays reset and clear any goa
            foreach (var phantom in _activePhantoms)
            {
                phantom.ResetState();
            }
        }

        protected Vector3 FindPhantoSpawnPosition(Vector3 playerHeadPos)
        {
            var stopwatch = Stopwatch.StartNew();
            Vector3 spawnPoint;
            var attempts = 0;

            while (true)
            {
                attempts++;

                var spawnBounds = _bounds.Value;
                // shrink the bounds by a foot to keep away from walls.
                spawnBounds.Expand(-0.3f);

                // Spherecast downwards to make sure there's enough room for the ghost..
                var randomPoint = spawnBounds.RandomPoint();
                var ray = new Ray(randomPoint, Vector3.down);

                if (!Physics.SphereCast(ray, 0.3f, out var hit, 2.0f, SceneMeshLayerMask,
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

            if (attempts > 10 || stopwatch.ElapsedMilliseconds > 10)
            {
                Debug.LogWarning($"Phanto spawn placement attempts: {attempts} took: {stopwatch.ElapsedMilliseconds}ms");
            }

            return spawnPoint;
        }

        protected void DestroyOriginCrystals()
        {
            foreach (var crystal in _originCrystals)
            {
                crystal.TargetsDestroyed -= OnCrystalTargetsDestroyed;
                crystal.Destruct();
            }

            _originCrystals.Clear();
        }

        private void OnCrystalTargetsDestroyed()
        {
            EndCrystalWave();
        }

        protected virtual void CreateSpawnPoints(int count)
        {
            Assert.IsNotNull(_sceneRoom);

            if (_originCrystals.Count > 0)
            {
                DestroyOriginCrystals();
            }

            var destination = _tableCrystalInstance.Position;

            // During the defeat phantoms wave there is an indestructible spawn point
            // so you have to defeat the whole wave.

            // find the navmesh verts closest to the corners of the room.
            var floor = _sceneRoom.Floor;
            var ceilingTransform = _sceneRoom.Ceiling.transform;
            var ceilingPlane = new Plane(ceilingTransform.forward, ceilingTransform.position);

            var spawnPoints = new List<(Vector3 point, float pathLength)>();

            foreach (var corner in floor.Boundary)
            {
                var cornerPos = floor.transform.TransformPoint(corner);

                if (!NavMesh.SamplePosition(cornerPos, out var navMeshHit, 10.0f, FloorAreaMask))
                {
                    continue;
                }

                var point = navMeshHit.position;

                // Verify this point in the navmesh is not under furniture and is reachable.
                if (!SceneQuery.VerifyPointIsOpen(point, ceilingPlane, SceneMeshLayerMask) ||
                    !SceneQuery.TryGetSqrPathLength(point, destination, out var length))
                {
                    continue;
                }

                spawnPoints.Add((point, length));
            }

            if (spawnPoints.Count < count)
            {
                // get all floor triangles.
                // Find a position on the floor that is furthest from the spawned table crystal.
                var floorTriangles = NavMeshBookKeeper.GetTrianglesWithId(FloorArea);
                Assert.IsTrue(floorTriangles.Count > 0);

                floorTriangles.RemoveAll((x) => !x.IsBorder);
                // There should always be border triangles in the navmesh.
                Assert.IsTrue(floorTriangles.Count > 0);

                foreach (var tri in floorTriangles)
                {
                    // furthest tri vertex from point
                    var point = tri.FurthestVert(destination);

                    if (!SceneQuery.VerifyPointIsOpen(point, ceilingPlane, SceneMeshLayerMask))
                    {
                        continue;
                    }

                    // prune points that are too close to each other.
                    var tooClose = false;
                    for (int i = 0; i < spawnPoints.Count; i++)
                    {
                        if (Vector3.Distance(spawnPoints[i].point, point) < 1.0f)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (tooClose)
                    {
                        continue;
                    }

                    // calculate path from vertex to crystal.
                    if (!SceneQuery.TryGetSqrPathLength(point, destination, out var pathLength))
                    {
                        continue;
                    }

                    spawnPoints.Add((point, pathLength));
                }
            }

            // sort spawn points by path length;
            spawnPoints.Sort((a, b) => b.pathLength.CompareTo(a.pathLength));

            count = Mathf.Min(count, spawnPoints.Count);

            for (var i = 0; i < count; i++)
            {
                var spawnPoint = spawnPoints[i].point;

                var shuffledTriangles = new List<NavMeshTriangle>();
                var triCount = NavMeshBookKeeper.TrianglesInRadius(spawnPoint, 0.05f, true, shuffledTriangles);

                if (triCount == 0)
                {
                    Debug.LogError("No navmesh triangles around point?");
                    SpawnCrystal(spawnPoint);
                    continue;
                }

                var removed = shuffledTriangles.RemoveAll((tri) => tri.areaId != FloorArea);

                if (shuffledTriangles.Count == 0)
                {
                    Debug.LogError("No floor triangles around point?");
                    SpawnCrystal(spawnPoint);
                    continue;
                }

                var padding = 0.025f;

                shuffledTriangles.Shuffle();
                // Find a point within these triangles that are > 1 cm and < 30 cm from the vertex.
                NavMeshBookKeeper.FindMatchingPoint(shuffledTriangles, out var result, (point, tri) =>
                {
                    var distance = Vector3.Distance(spawnPoint, point);

                    if (distance < 0.01f || distance > OneFoot)
                    {
                        return false;
                    }

                    return NavMesh.FindClosestEdge(point, out var navMeshHit, tri.AreaMask) &&
                           navMeshHit.distance <= padding;
                });

                spawnPoint = result;

                SpawnCrystal(spawnPoint);
            }
        }

        protected void SpawnCrystal(Vector3 point)
        {
            var crystal = Instantiate(originCrystalPrefab, point,
                Quaternion.AngleAxis(Random.Range(0f, 180f), Vector3.up));

            crystal.Initialize();
            crystal.TargetsDestroyed += OnCrystalTargetsDestroyed;

            _originCrystals.Enqueue(crystal);
        }

        private void DeactivateCrystals()
        {
            foreach (var target in PhantomTarget.AvailableTargets)
            {
                if (target is CrystalRangedTarget crystalTarget)
                {
                    crystalTarget.Activate(false);
                    crystalTarget.Reveal(false);
                }
            }
        }

        protected void RemoveGoo()
        {
            if (_extinguishGooCoroutine != null)
            {
                StopCoroutine(_extinguishGooCoroutine);
            }

            _extinguishGooCoroutine = StartCoroutine(PhantoGoo.ExtinguishAllGoo());
        }

        protected void ActivateCrystals()
        {
            foreach (var target in PhantomTarget.AvailableTargets)
            {
                if (target is CrystalRangedTarget crystalTarget)
                {
                    crystalTarget.Activate(true);
                    crystalTarget.Reveal();
                }
            }

            FurnitureNavMeshGenerator result;
            Vector3 crystalSpawnPoint;

            // pick a piece of furniture in the room to spawn a "table" crystal on.
            if (SceneQuery.TryGetFurniture(CrystalSurfaces, out result))
            {
                crystalSpawnPoint = result.RandomPoint();
            }
            else
            {
                // in this case there's no table/couch/bed to target, so crystal is at a random point in the room.
                crystalSpawnPoint = SceneQuery.RandomPointOnFurniture();
            }

            _tableCrystalInstance = Instantiate(tableCrystalPrefab, crystalSpawnPoint,
                Quaternion.AngleAxis(Random.Range(0, 180), Vector3.up), transform);
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

        public void SpawnOuch(Vector3 position, Vector3 normal)
        {
            var fleeNormal = Vector3.ProjectOnPlane(normal, Vector3.up).normalized;

            // If there's already a flee target nearby, update it instead of adding one.
            if (PhantomFleeTarget.TryGetFleeTarget(position, out var fleeTarget))
            {
                // update the normal of the existing ouch
                fleeTarget.UpdateDirection(fleeNormal);

                return;
            }

            if (!_ouchPool.TryDequeue(out fleeTarget))
            {
                Debug.LogWarning("ouch queue empty!");
                return;
            }

            fleeTarget.SetPositionAndDirection(position, fleeNormal);
            fleeTarget.Show();

            _ouchPool.Enqueue(fleeTarget);
        }

        private IEnumerator TargetSpawner()
        {
            yield return new WaitUntil(() => GameplaySettingsManager.Instance != null);

            var wait = new WaitForSeconds(targetRefreshRate);

            while (!_started) yield return wait;

            while (enabled)
            {
                if (_yumPool.Count > 0)
                {
                    var yum = _yumPool.Dequeue();
                    yum.Dispatch(SceneQuery.RandomPointOnFurniture());

                    yield return wait;
                }

                yield return wait;
            }
        }

        protected IEnumerator PhantomSpawner()
        {
            yield return new WaitUntil(() => GameplaySettingsManager.Instance != null);

            while (!_started) yield return _spawnWait;

            while (_phantomPool.Count > 0 && _activePhantoms.Count < _phantomWaveCount)
            {
                yield return _spawnWait;

                switch (WinCondition)
                {
                    case GameplaySettings.WinCondition.DefeatPhanto:
                        SpawnFromGoo();
                        break;
                    case GameplaySettings.WinCondition.DefeatAllPhantoms:
                        SpawnFromCrystal();
                        break;
                }
            }

            _phantomSpawnerCoroutine = null;
        }

        private void SpawnFromCrystal()
        {
            // queue is empty or crystal hasn't been spawned.
            if (_originCrystals.Count == 0 || !_phantomPool.TryDequeue(out var phantom))
            {
                return;
            }

            _originCrystals.TryDequeue(out var crystal);

            phantom.Respawn(crystal.Position);
            phantom.SetOriginCrystal(crystal);

            _activePhantoms.Add(phantom);

            _originCrystals.Enqueue(crystal);
        }

        private void SpawnFromGoo()
        {
            // find a spot to spawn the phantom
            // near a goo, on the navmesh.
            var goos = PhantoGoo.ActiveGoos;
            _spawnLocations.Clear();

            foreach (var goo in goos)
                if (SceneQuery.TryGetClosestPoint(goo.Position, out var point, 0.3f))
                    _spawnLocations.Add(point);

            // either there's no furniture or floor point on goo, or queue got emptied.
            if (_spawnLocations.Count == 0 || !_phantomPool.TryDequeue(out var phantom)) return;

            var position = _spawnLocations.RandomElement();

            phantom.Respawn(position);
            _activePhantoms.Add(phantom);
        }

        public void CreateNavMeshLink(Vector3[] pathCorners, int pathCornerCount, Vector3 destination, int areaId)
        {
            _navMeshGenerator.CreateNavMeshLink(pathCorners, pathCornerCount, destination, areaId);
        }

        public NavMeshTriangle RandomFloorTriangle()
        {
            return _navMeshGenerator.RandomFloorTriangle();
        }
    }
}
