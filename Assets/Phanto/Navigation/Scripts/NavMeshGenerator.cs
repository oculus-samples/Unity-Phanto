// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Phanto.Enemies.DebugScripts;
using PhantoUtils;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using static NavMeshGenerateLinks;

/// <summary>
///     Handles nav mesh generation from scene data.
/// </summary>
public class NavMeshGenerator : MonoBehaviour
{
    private static readonly Dictionary<OVRSceneRoom, NavMeshGenerator> NavMeshGenerators = new Dictionary<OVRSceneRoom, NavMeshGenerator>();

    private const float EdgeExpansion = NavMeshConstants.OneFoot;

    private static List<NavMeshTriangle> _navMeshTriangles;

    [SerializeField] [Range(0.001f, 10.0f)]
    private float volumeHeight = 0.03f;

    // Careful, InteractionSDK has a NavMeshSurface type too!
    [SerializeField] private NavMeshSurface navMeshSurface;

    [SerializeField] private NavMeshLinkController navMeshLinkPrefab;

    private readonly HashSet<NavMeshLinkController> _navMeshLinks = new();

    private readonly List<NavMeshTriangle> _validTriangles = new();

    private OVRSceneRoom _sceneRoom;

    public NavMeshSurface FloorNavMeshSurface { get; private set; }

    public Plane CeilingPlane { get; private set; }
    public Plane FloorPlane { get; private set; }

    private void Awake()
    {
        FloorNavMeshSurface = navMeshSurface;

        NavMeshBookKeeper.OnValidateScene += GenerateInternalLinks;
    }

    private void OnEnable()
    {
        DebugDrawManager.DebugDrawEvent += DebugDraw;
    }

    private void OnDisable()
    {
        DebugDrawManager.DebugDrawEvent -= DebugDraw;
    }

    private void OnDestroy()
    {
        NavMeshGenerators.Remove(_sceneRoom);

        NavMeshBookKeeper.OnValidateScene -= GenerateInternalLinks;

        foreach (var nml in _navMeshLinks)
            if (nml != null)
                nml.Destruct();

        navMeshSurface.ClearNavMeshTriangles();
    }

    public void Initialize(OVRSceneRoom room, Bounds meshBounds, bool furnitureInScene)
    {
        _sceneRoom = room;

        NavMeshGenerators[room] = this;

        Assert.IsNotNull(room.Ceiling);
        Assert.IsNotNull(room.Floor);

        var ceilingTransform = room.Ceiling.transform;
        CeilingPlane = new Plane(ceilingTransform.forward, ceilingTransform.position);

        var floorTransform = room.Floor.transform;
        FloorPlane = new Plane(floorTransform.forward, floorTransform.position);

        var floorPoint = floorTransform.position;

        transform.SetPositionAndRotation(floorPoint, Quaternion.LookRotation(floorTransform.up, Vector3.up));

        if (furnitureInScene)
            floorPoint.y += volumeHeight;
        else
            // if there's no furniture in the scene extend the navmesh volume to encompass 1/2 of the room.
            // this should provide some walkable surfaces on unmarked furniture.
            floorPoint.y += (room.Ceiling.transform.position.y - room.Floor.transform.position.y) * 0.5f;

        var navMeshVolumeBounds = new Bounds(floorPoint, Vector3.zero);
        navMeshVolumeBounds.Encapsulate(meshBounds.min);
        navMeshVolumeBounds.Encapsulate(new Vector3(meshBounds.max.x, meshBounds.min.y, meshBounds.max.z));

        BuildNavMesh(navMeshVolumeBounds);
    }

    public Vector3 RandomPointOnFloor(Vector3 position, float minDistance = 1.0f, bool verifyOpenArea = true)
    {
        var iterations = 0;

        NavMeshTriangle triangle;
        Vector3 point;

        do
        {
            triangle = RandomFloorTriangle();

            if (triangle == null) return default;

            point = triangle.GetRandomPoint();

            if (Vector3.Distance(position, point) > minDistance
                && (!verifyOpenArea || triangle.IsOpen))
                break;
        } while (iterations++ < 10);

        return point;
    }

    /// <summary>
    ///     When path to destination is incomplete add extra links to path.
    /// </summary>
    public void CreateNavMeshLink(IReadOnlyList<Vector3> corners, int cornerCount, Vector3 destination, int areaId)
    {
        var path = new NavMeshPath();

        var startPoint = corners[cornerCount - 1];
        if (cornerCount > 1) startPoint = Vector3.Lerp(corners[cornerCount - 2], startPoint, 0.9f);

        // path going the other way
        var success = NavMesh.CalculatePath(destination, startPoint, NavMesh.AllAreas, path);
        Vector3 endPoint;
        if (!success || path.status == NavMeshPathStatus.PathInvalid)
        {
            NavMesh.Raycast(destination, startPoint, out var hit, NavMesh.AllAreas);

            endPoint = Vector3.Lerp(destination, hit.position, 0.9f);
        }
        else
        {
            var reversePath = new Vector3[1024];
            cornerCount = path.GetCornersNonAlloc(reversePath);

            endPoint = reversePath[cornerCount - 1];
            if (cornerCount > 1) endPoint = Vector3.Lerp(reversePath[cornerCount - 2], endPoint, 0.9f);
        }

        Assert.IsTrue(endPoint.IsSafeValue());
        Assert.IsFalse(startPoint.Approximately(endPoint));

        var link = Instantiate(navMeshLinkPrefab, transform);
        link.Initialize(startPoint, endPoint, areaId);
    }

    internal void BuildNavMesh(Bounds volumeBounds)
    {
        volumeBounds.Expand(new Vector3(EdgeExpansion, 0.002f, EdgeExpansion));

        navMeshSurface.size = volumeBounds.size;
        navMeshSurface.center = transform.InverseTransformPoint(volumeBounds.center);

        _navMeshTriangles = navMeshSurface.GenerateNavMeshTriangles();

        Assert.IsNotNull(_navMeshTriangles, "The floor has no navmesh!");
    }

    private void GenerateInternalLinks()
    {
        ValidateTriangles();
        GenInternalLinks(_navMeshTriangles, navMeshLinkPrefab, transform);
    }

    private void ValidateTriangles()
    {
        foreach (var tri in _navMeshTriangles)
            if (tri.IsOpen)
                _validTriangles.Add(tri);

        if (_validTriangles.Count == 0)
        {
            Debug.LogWarning("Floor is entirely covered.");
            _validTriangles.AddRange(_navMeshTriangles);
        }
    }

    public NavMeshTriangle RandomFloorTriangle()
    {
        if (_validTriangles.Count == 0) ValidateTriangles();

        Assert.IsNotNull(_navMeshTriangles, "The floor has no navmesh!");

        return _validTriangles.RandomElement();
    }

    public NavMeshTriangle ClosestFloorTriangle(Vector3 point)
    {
        if (_validTriangles.Count == 0) ValidateTriangles();

        var result = _validTriangles[0];
        var plane = new Plane(Vector3.up, result.center);

        point = plane.ClosestPointOnPlane(point);

        var minDistance = float.MaxValue;

        foreach (var tri in _validTriangles)
        {
            var distance = Vector3.Distance(tri.center, point);

            if (distance < minDistance)
            {
                minDistance = distance;
                result = tri;
            }
        }

        return result;
    }

    /// <summary>
    ///     Find triangle closest to the center of this list of navmesh triangle.
    /// </summary>
    /// <param name="triangles"></param>
    /// <returns></returns>
    public static NavMeshTriangle FindCenter(List<NavMeshTriangle> triangles)
    {
        var center = Vector3.zero;

        foreach (var tri in triangles) center += tri.center;

        center /= triangles.Count;

        var result = triangles[0];
        var minDistance = float.MaxValue;

        foreach (var tri in triangles)
        {
            var distance = Vector3.Distance(tri.center, center);

            if (distance < minDistance)
            {
                minDistance = distance;
                result = tri;
            }
        }

        return result;
    }

    public static bool TryGetNavMeshGenerator(OVRSceneRoom room, out NavMeshGenerator result)
    {
        return NavMeshGenerators.TryGetValue(room, out result);
    }

    private void DebugDraw()
    {
        if (_navMeshTriangles != null)
            foreach (var triangle in _navMeshTriangles)
                triangle.DebugDraw(Color.yellow, Color.grey);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        OnValidate();
    }

    private void OnValidate()
    {
        if (navMeshSurface == null) navMeshSurface = GetComponent<NavMeshSurface>();
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(NavMeshGenerator))]
public class NavMeshGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (EditorApplication.isPlaying)
        {
            GUILayout.Space(16);
            if (GUILayout.Button("Bake"))
            {
                var room = FindObjectOfType<OVRSceneRoom>();

                var floorTransform = room.Floor.transform;
                var bounds = new Bounds(floorTransform.position, Vector3.zero);
                bounds.Encapsulate(floorTransform.TransformPoint(room.Floor.Dimensions / 2.0f));
                bounds.Encapsulate(floorTransform.TransformPoint(room.Floor.Dimensions / -2.0f));
                bounds.Expand(0.3f);

                var generator = target as NavMeshGenerator;
                generator.BuildNavMesh(bounds);
            }
        }
    }
}
#endif
