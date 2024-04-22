// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Phanto.Audio.Scripts;
using Phanto.Enemies.DebugScripts;
using Phantom.Environment.Scripts;
using PhantoUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using Utilities.XR;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using static NavMeshConstants;

namespace Phantom
{
    public class SceneQueryDemoManager : MonoBehaviour, IPhantomManager, ICollisionDemo
    {
        private const float CELL_SIZE = 0.10f;
        private const int LINE_POINT_COUNT = 256;

        [SerializeField] private PhantomController phantomPrefab;
        [SerializeField] private TableCrystal tableCrystalPrefab;
        [SerializeField] private OriginCrystal originCrystalPrefab;

        [SerializeField] private float spawnDelay = 1.5f;
        [SerializeField] private int spawnPointCount = 2;
        [SerializeField] private int spawnCount = 20;

        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        [SerializeField] private SemanticSoundMapCollection soundMapCollection;
        [SerializeField] protected bool debugDraw = true;

        [SerializeField] protected BouncingPhantomController bouncingPrefab;
        private readonly Queue<BouncingPhantomController> _bouncingPool = new();

        private readonly HashSet<PhantomController> _activePhantoms = new();
        private readonly HashSet<PhantomTarget> _allPhantomTargets = new();
        private readonly Vector3 _cellSize = Vector3.one * CELL_SIZE;
        private readonly List<Vector3> _cubes = new List<Vector3>(1024);

        private readonly Dictionary<MonoBehaviour, Stopwatch> _debugDrawSceneElements =
            new Dictionary<MonoBehaviour, Stopwatch>();

        private readonly Vector3[] _linePoints = new Vector3[LINE_POINT_COUNT];
        private readonly Queue<OriginCrystal> _originCrystals = new Queue<OriginCrystal>();
        private readonly Queue<PhantomController> _phantomPool = new();
        private readonly Queue<(Vector3 pos, Vector3 normal, long ms)> _pointQueue = new();
        private readonly Stopwatch _queueTimer = Stopwatch.StartNew();

        private readonly Dictionary<string, Queue<PhantoRandomOneShotSfxBehavior>> _semanticSfxDictionary =
            new Dictionary<string, Queue<PhantoRandomOneShotSfxBehavior>>();

        private readonly SpatialHash<long> _spatialHash = new(CELL_SIZE);
        private readonly Stopwatch _spatialHashStopwatch = Stopwatch.StartNew();
        private Bounds? _bounds;

        private NavMeshGenerator _navMeshGenerator;

        private Coroutine _phantomSpawnerCoroutine;

        private bool _sceneReady;
        private OVRSceneRoom _sceneRoom = null;
        private WaitForSeconds _spawnWait = new WaitForSeconds(1);

        private bool _started;

        private TableCrystal _tableCrystalInstance;
        private readonly List<ContactPoint> _contactPoints = new(256);

        public GameplaySettings.WinCondition WinCondition => GameplaySettings.WinCondition.DefeatAllPhantoms;

        protected void Awake()
        {
            DebugDrawManager.DebugDraw = debugDraw;

            PopulateSemanticSfxPool(8);
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

        private readonly List<FurnitureNavMeshGenerator> _availableFurniture = new List<FurnitureNavMeshGenerator>();
        private int _crystalSpawnIndex = 0;

        private IEnumerator Start()
        {
            while (_navMeshGenerator == null)
            {
                yield return null;
                _navMeshGenerator = FindObjectOfType<NavMeshGenerator>();
            }

            for (var i = 0; i < spawnCount; i++)
            {
                var phantom = Instantiate(bouncingPrefab, Vector3.zero,
                    Quaternion.identity);
                phantom.Initialize(this);
                phantom.Hide();
                _bouncingPool.Enqueue(phantom);
            }

            while (!_bounds.HasValue || _sceneRoom == null) yield return null;

            for (var i = 0; i < spawnCount; i++)
            {
                // spawn npc at point.
                var phantom = Instantiate(phantomPrefab, Vector3.zero,
                    Quaternion.AngleAxis(Random.Range(0.0f, 360.0f), Vector3.up));
                phantom.Initialize(this);
                // _phantomPool.Add(phantom);
                ReturnToPool(phantom);
            }

            SceneQuery.GetFurnitureWithClassifications(PhantomManager.CrystalSurfaces,
                _availableFurniture);

            _availableFurniture.Shuffle();

            _started = true;
            SetupCrystals();
        }

        private void Update()
        {
            if (!_sceneReady || !_started)
            {
                return;
            }

            XRGizmos.DrawPointer(leftHand.position, leftHand.forward, Color.blue, 0.1f);
            XRGizmos.DrawPointer(rightHand.position, rightHand.forward, Color.red, 0.1f);

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
            {
                var ray = new Ray(leftHand.position, leftHand.forward);

                ThrowPhantom(ray);
            }

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                var ray = new Ray(rightHand.position, rightHand.forward);

                ThrowPhantom(ray);
            }
        }

        public void SpawnOuch(Vector3 position, Vector3 normal)
        {
        }

        public void DecrementPhantom()
        {
        }

        public void CreateNavMeshLink(Vector3[] pathCorners, int pathCornerCount, Vector3 destination, int areaId)
        {
            _navMeshGenerator.CreateNavMeshLink(pathCorners, pathCornerCount, destination, areaId);
        }

        /// <summary>
        /// Must be subscribed to PhantoScenePostProcessor event.
        /// </summary>
        /// <param name="sceneRoot"></param>
        public void OnSceneDataProcessed(Transform sceneRoot)
        {
            _sceneRoom = sceneRoot.GetComponentInChildren<OVRSceneRoom>();

            Assert.IsNotNull(_sceneRoom);
        }

        public void ReturnToPool(PhantomController phantom)
        {
            _activePhantoms.Remove(phantom);

            phantom.Hide();
            _phantomPool.Enqueue(phantom);
        }

        private void StartPhantomSpawner(bool start = true)
        {
            if (_phantomSpawnerCoroutine != null)
            {
                StopCoroutine(_phantomSpawnerCoroutine);
                _phantomSpawnerCoroutine = null;
            }

            if (start)
            {
                _phantomSpawnerCoroutine = StartCoroutine(PhantomSpawner());
            }
        }

        private void SetupCrystals()
        {
            _spawnWait = new WaitForSeconds(spawnDelay);

            StartPhantomSpawner(false);

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

            // spawn window/door crystals.
            // spawn a table crystal
            ActivateCrystal();

            // Create a spawn point that can't be removed for the phantoms.
            CreateSpawnPoints(spawnPointCount);

            StartPhantomSpawner();

            // any existing phantoms need to have their attack delays reset and clear any goal
            foreach (var phantom in _activePhantoms)
            {
                phantom.ResetState();
            }
        }

        private void ActivateCrystal()
        {
            foreach (var target in PhantomTarget.AvailableTargets)
            {
                if (target is CrystalRangedTarget crystalTarget)
                {
                    crystalTarget.Activate(true);
                    crystalTarget.Reveal();
                }
            }

            Vector3 crystalSpawnPoint;

            // pick a piece of furniture in the room to spawn a "table" crystal on.
            if (_availableFurniture.Count != 0)
            {
                var furniture = _availableFurniture[_crystalSpawnIndex];
                Assert.IsNotNull(furniture);
                crystalSpawnPoint = furniture.RandomPoint();

                if (++_crystalSpawnIndex >= _availableFurniture.Count)
                {
                    _availableFurniture.Shuffle();
                    _crystalSpawnIndex = 0;
                }
            }
            else
            {
                // in this case there's no table/couch/bed to target, so crystal is at a random point in the room.
                crystalSpawnPoint = SceneQuery.RandomPointOnFurniture();
            }

            _tableCrystalInstance = Instantiate(tableCrystalPrefab, crystalSpawnPoint,
                Quaternion.AngleAxis(Random.Range(0, 180), Vector3.up), transform);
        }

        private void CreateSpawnPoints(int count)
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

        private void DestroyOriginCrystals()
        {
            foreach (var crystal in _originCrystals)
            {
                crystal.TargetsDestroyed -= OnCrystalTargetsDestroyed;
                crystal.Destruct();
            }

            _originCrystals.Clear();
        }

        private void SpawnCrystal(Vector3 point)
        {
            var crystal = Instantiate(originCrystalPrefab, point,
                Quaternion.AngleAxis(Random.Range(0f, 180f), Vector3.up));

            crystal.Initialize();
            crystal.TargetsDestroyed += OnCrystalTargetsDestroyed;

            _originCrystals.Enqueue(crystal);
        }

        private void OnCrystalTargetsDestroyed()
        {
            SetupCrystals();
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

        private void ThrowPhantom(Ray ray)
        {
            if (!_bouncingPool.TryDequeue(out var phantom)) return;

            phantom.Launch(ray);

            _bouncingPool.Enqueue(phantom);
        }

        private IEnumerator PhantomSpawner()
        {
            while (!_started)
            {
                yield return null;
            }

            while (_phantomPool.Count > 0 && _activePhantoms.Count < spawnCount)
            {
                yield return _spawnWait;

                SpawnFromCrystal();
            }

            _phantomSpawnerCoroutine = null;
        }

        private void SpawnFromCrystal()
        {
            Assert.IsTrue(_originCrystals.Count > 0);

            // queue is empty?
            if (!_phantomPool.TryDequeue(out var phantom))
            {
                return;
            }

            _originCrystals.TryDequeue(out var crystal);

            phantom.Respawn(crystal.Position);
            phantom.SetOriginCrystal(crystal);

            _activePhantoms.Add(phantom);

            _originCrystals.Enqueue(crystal);
        }

        private void OnBoundsChanged(Bounds bounds)
        {
            _bounds = bounds;
            _sceneReady = true;
        }

        public void RenderCollision(Collision collision)
        {
            var count = collision.GetContacts(_contactPoints);

            if (count == 0)
            {
                return;
            }

            var primary = _contactPoints[0];
            var (pos, normal) = (primary.point, primary.normal);

            if (!SceneQuery.TryGetClosestSemanticClassification(pos, normal, out var classification))
            {
                Debug.LogWarning($"SceneQuery failed to find object at: {pos.ToString("F2")}");
                return;
            }

            // Look up what we collided with
            // play associated sound
            PlaySemanticSfx(pos, classification);

            // debug draw associated plane/volume.
            if (SceneQuery.TryGetVolume(classification, out var volume))
            {
                if (!_debugDrawSceneElements.TryGetValue(volume, out var stopwatch))
                {
                    _debugDrawSceneElements[volume] = Stopwatch.StartNew();
                }
                else
                {
                    stopwatch.Restart();
                }
            }
            else if (SceneQuery.TryGetPlane(classification, out var plane))
            {
                if (!_debugDrawSceneElements.TryGetValue(plane, out var stopwatch))
                {
                    _debugDrawSceneElements[plane] = Stopwatch.StartNew();
                }
                else
                {
                    stopwatch.Restart();
                }
            }

#if false
            for (var i = 0; i < count; i++)
            {
                // debug draw contact point/normal.
                var contact = _contactPoints[i];
                _pointQueue.Enqueue((contact.point, contact.normal, _queueTimer.ElapsedMilliseconds));
            }
#endif

            // add to spatial hash grid.
            _spatialHash.Clear(pos);
            _spatialHash.Add(pos, _spatialHashStopwatch.ElapsedMilliseconds);
        }

        private void PlaySemanticSfx(Vector3 point, OVRSemanticClassification classification)
        {
            // Play a sound based on what you landed on.
            var label = classification.Labels[0];

            if (string.IsNullOrEmpty(label) || !_semanticSfxDictionary.TryGetValue(label, out var queue))
            {
                return;
            }

            if (queue.TryDequeue(out var oneShot))
            {
                oneShot.PlaySfxAtPosition(point);
                queue.Enqueue(oneShot);
            }
        }

        private void DebugDraw()
        {
            const long duration = 5000;
            var currentMs = _queueTimer.ElapsedMilliseconds;

            while (_pointQueue.TryPeek(out var peek))
            {
                if (peek.ms > currentMs - duration) break;

                _pointQueue.Dequeue();
            }

            foreach (var (position, normal, _) in _pointQueue)
            {
                var rotation = Quaternion.FromToRotation(Vector3.up, normal);

                XRGizmos.DrawCircle(position, rotation, 0.05f, MSPalette.BlueViolet);
                XRGizmos.DrawPointer(position, normal, MSPalette.Goldenrod, 0.05f, 0.005f);
            }

            foreach (var (element, stopwatch) in _debugDrawSceneElements)
            {
                if (stopwatch.ElapsedMilliseconds > duration * 1.5f || element == null ||
                    !element.gameObject.activeInHierarchy)
                {
                    continue;
                }

                switch (element)
                {
                    case OVRSceneVolume volume:
                        DebugDraw(volume);
                        break;
                    case OVRScenePlane plane:
                        DebugDraw(plane);
                        break;
                }
            }

            DebugDrawSpatialHash();
        }

        private void DebugDraw(OVRSceneVolume sceneVolume)
        {
            var volumeTransform = sceneVolume.transform;
            var dimensions = sceneVolume.Dimensions;
            var pos = volumeTransform.position;

            pos.y -= dimensions.z * 0.5f;

            if (sceneVolume.OffsetChildren)
            {
                var offset = sceneVolume.Offset;
                (offset.x, offset.y, offset.z) = (-offset.x, offset.z, offset.y);

                pos += offset;
            }

            XRGizmos.DrawWireCube(pos, volumeTransform.rotation, dimensions, Color.red);
        }

        private void DebugDraw(OVRScenePlane scenePlane)
        {
            var planeTransform = scenePlane.transform;
            var pointCount = Mathf.Min(scenePlane.Boundary.Count, LINE_POINT_COUNT);

            for (int i = 0; i < pointCount; i++)
            {
                _linePoints[i] = planeTransform.TransformPoint(scenePlane.Boundary[i]);
            }

            XRGizmos.DrawLineList(_linePoints, Color.blue, true, pointCount);
        }

        private void DebugDrawSpatialHash()
        {
            const int duration = 10000;

            _cubes.Clear();

            foreach (var cell in _spatialHash.Cells)
            {
                _spatialHash.TryGetCellContents(cell, out var values);

                var time = values.First();

                if (time < _spatialHashStopwatch.ElapsedMilliseconds - duration)
                {
                    continue;
                }

                _cubes.Add(_spatialHash.CellToWorld(cell));
            }

            // FIXME: (performance) This can draw redundant lines
            XRGizmos.DrawWireCubes(_cubes, Quaternion.identity, _cellSize, Color.cyan);
        }

        private void PopulateSemanticSfxPool(int size)
        {
            foreach (var queue in _semanticSfxDictionary.Values)
            {
                foreach (var entry in queue)
                {
                    Destroy(entry.gameObject);
                }
                queue.Clear();
            }
            _semanticSfxDictionary.Clear();

            var parent = transform;

            var count = 0;

            foreach (var soundMap in soundMapCollection.SoundMaps)
            {
                var label = soundMap.name;
                var prefab = soundMap.oneShotPrefab;

                if (string.IsNullOrEmpty(label) || prefab == null)
                {
                    continue;
                }

                var queue = new Queue<PhantoRandomOneShotSfxBehavior>();

                for (int i = 0; i < size; i++)
                {
                    var instance = Instantiate(prefab, parent);
                    instance.gameObject.SetSuffix($"OneShot {count++:000}");
                    queue.Enqueue(instance);
                }

                _semanticSfxDictionary.TryAdd(label, queue);
            }
        }
    }
}
