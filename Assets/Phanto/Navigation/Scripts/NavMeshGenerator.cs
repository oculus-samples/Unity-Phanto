// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using Phanto.Enemies.DebugScripts;
using Phantom.Environment.Scripts;
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
    private const float EdgeExpansion = NavMeshConstants.OneFoot;

    private static readonly Dictionary<Object, NavMeshGenerator> NavMeshGenerators =
        new Dictionary<Object, NavMeshGenerator>();

    private static readonly Dictionary<PhantoAnchorInfo, PhantoAnchorInfo> LinkedOpenings =
        new Dictionary<PhantoAnchorInfo, PhantoAnchorInfo>();

    private static readonly List<NavMeshGenerator> Floors = new List<NavMeshGenerator>();

    private List<NavMeshTriangle> _navMeshTriangles;

    [SerializeField]
    [Range(0.001f, 10.0f)]
    private float volumeHeight = 0.03f;

    // Careful, InteractionSDK has a NavMeshSurface type too!
    [SerializeField] private NavMeshSurface navMeshSurface;

    [SerializeField] private NavMeshLinkController navMeshLinkPrefab;

    private readonly HashSet<NavMeshLinkController> _navMeshLinks = new();

    private readonly List<NavMeshTriangle> _validTriangles = new();

    private MRUKRoom _sceneRoom;
    private readonly List<PhantoAnchorInfo> _openings = new List<PhantoAnchorInfo>();

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

        _sceneRoom = GetComponentInParent<MRUKRoom>();
        Assert.IsNotNull(_sceneRoom);

        NavMeshGenerators.Add(_sceneRoom, this);
        NavMeshGenerators.Add(_sceneRoom.FloorAnchor, this);
        NavMeshGenerators.Add(transform, this);
        NavMeshGenerators.Add(gameObject, this);
    }

    private void OnDisable()
    {
        DebugDrawManager.DebugDrawEvent -= DebugDraw;

        Assert.IsNotNull(_sceneRoom);

        NavMeshGenerators.Remove(_sceneRoom);
        NavMeshGenerators.Remove(_sceneRoom.FloorAnchor);
        NavMeshGenerators.Remove(transform);
        NavMeshGenerators.Remove(gameObject);
    }

    private void OnDestroy()
    {
        NavMeshBookKeeper.OnValidateScene -= GenerateInternalLinks;

        foreach (var nml in _navMeshLinks)
            if (nml != null)
                nml.Destruct();

        navMeshSurface.ClearNavMeshTriangles();
    }

    public void Initialize(MRUKRoom room, Bounds meshBounds, bool furnitureInScene)
    {
        _sceneRoom = room;

        Assert.IsNotNull(room.CeilingAnchor);
        Assert.IsNotNull(room.FloorAnchor);

        var ceilingTransform = room.CeilingAnchor.transform;
        CeilingPlane = new Plane(ceilingTransform.forward, ceilingTransform.position);

        var floorTransform = room.FloorAnchor.transform;
        FloorPlane = new Plane(floorTransform.forward, floorTransform.position);

        var floorPoint = floorTransform.position;

        transform.SetPositionAndRotation(floorPoint, Quaternion.LookRotation(floorTransform.up, Vector3.up));

        if (furnitureInScene)
            floorPoint.y += volumeHeight;
        else
            // if there's no furniture in the scene extend the navmesh volume to encompass 1/2 of the room.
            // this should provide some walkable surfaces on unmarked furniture.
            floorPoint.y += (room.CeilingAnchor.transform.position.y - room.FloorAnchor.transform.position.y) * 0.5f;

        var navMeshVolumeBounds = new Bounds(floorPoint, Vector3.zero);

        foreach (var point in room.FloorAnchor.PlaneBoundary2D)
        {
            navMeshVolumeBounds.Encapsulate(floorTransform.TransformPoint(point));
        }

        navMeshVolumeBounds.Encapsulate(meshBounds.min);
        navMeshVolumeBounds.Encapsulate(new Vector3(meshBounds.max.x, meshBounds.min.y, meshBounds.max.z));

        BuildNavMesh(navMeshVolumeBounds);
    }

    public Vector3 RandomPointOnFloor(Vector3 position, float minDistance = 1.0f, bool verifyOpenArea = true)
    {
        var shuffledTriangles = new List<NavMeshTriangle>(_navMeshTriangles);

        // Search the room up to 10 times for a valid spawn point.
        for (int i = 0; i < 10; i++)
        {
            shuffledTriangles.Shuffle();

            foreach (var triangle in shuffledTriangles)
            {
                if (triangle == null)
                {
                    continue;
                }

                var point = triangle.GetRandomPoint();

                if (Vector3.Distance(position, point) > minDistance
                    && (!verifyOpenArea || triangle.IsOpen))
                {
                    return point;
                }
            }
        }

        return position;
    }

    public void CreateNavMeshLink(IReadOnlyList<Vector3> corners, int cornerCount, Vector3 destination, int areaId)
    {
        var room = SceneQuery.GetRoomContainingPoint(destination);
        if (!NavMeshGenerators.TryGetValue(room, out var floor))
        {
            Debug.LogError("No floor navmesh associated with room.", room);

            return;
        }

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

        var link = Instantiate(floor.navMeshLinkPrefab, floor.transform);
        link.Initialize(startPoint, endPoint, areaId);
    }

    internal void BuildNavMesh(Bounds volumeBounds)
    {
        volumeBounds.Expand(new Vector3(EdgeExpansion, 0.002f, EdgeExpansion));

        navMeshSurface.size = transform.InverseTransformVector(volumeBounds.size);
        navMeshSurface.center = transform.InverseTransformPoint(volumeBounds.center);

        _navMeshTriangles = navMeshSurface.GenerateNavMeshTriangles();

        Assert.IsNotNull(_navMeshTriangles, "The floor has no navmesh!");
    }

    private void GenerateInternalLinks()
    {
        ValidateTriangles();

        GenerateDoorLinks();
        GenInternalLinks(_navMeshTriangles, navMeshLinkPrefab, transform);
    }

    private void GenerateDoorLinks()
    {
        _openings.Clear();
        _sceneRoom.GetComponentsInChildren(true, _openings);

        _openings.RemoveAll((classification) => !classification.ContainsAny(SceneQuery.Openings));

        foreach (var opening in _openings)
        {
            if (!opening.IsPlane || LinkedOpenings.ContainsKey(opening))
            {
                continue;
            }

            // find which room is on the positive side and negative side of the door
            if (!SceneQuery.TryGetConnectingRoom(_sceneRoom, opening, out Vector3 front, out Vector3 back, out var otherRoom))
            {
                // opening doesn't connect to different room.
                continue;
            }

            // if they are different rooms place a navmesh link to connect them.

            // snap both points to navmesh.
            if (!NavMesh.SamplePosition(front, out var frontHit, NavMeshConstants.OneFoot,
                    NavMeshConstants.FloorAreaMask)
                || !NavMesh.SamplePosition(back, out var backHit, NavMeshConstants.OneFoot,
                    NavMeshConstants.FloorAreaMask))
            {
                continue;
            }

            front = frontHit.position;
            back = backHit.position;

            // If there's a matching door in the other room
            // add it to the linked openings dictionary so we can skip it
            // when processing the other room.
            if (SceneQuery.TryGetLinkedOpening(opening, otherRoom, out var sibling))
            {
                LinkedOpenings[opening] = sibling;
                LinkedOpenings[sibling] = opening;
            }

            var link = Instantiate(navMeshLinkPrefab, opening.transform);
            link.Initialize(front, back);
        }
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
        Floors.Clear();
        Floors.AddRange(NavMeshGenerators.Values);

        var floor = Floors.RandomElement();

        Assert.IsNotNull(floor);

        if (floor._validTriangles.Count == 0) floor.ValidateTriangles();

        Assert.IsNotNull(floor._navMeshTriangles, "The floor has no navmesh!");

        return floor._validTriangles.RandomElement();
    }

    public static bool TryGetNavMeshGenerator(MRUKRoom room, out NavMeshGenerator result)
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
                var room = FindFirstObjectByType<MRUKRoom>();

                var floorTransform = room.FloorAnchor.transform;
                var bounds = new Bounds(floorTransform.position, Vector3.zero);
                bounds.Encapsulate(floorTransform.TransformPoint(room.FloorAnchor.PlaneRect.Value.size / 2.0f));
                bounds.Encapsulate(floorTransform.TransformPoint(room.FloorAnchor.PlaneRect.Value.size / -2.0f));
                bounds.Expand(0.3f);

                var generator = target as NavMeshGenerator;
                generator.BuildNavMesh(bounds);
            }
        }
    }
}
#endif
