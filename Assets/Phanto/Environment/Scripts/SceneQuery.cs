// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using System;
using System.Collections.Generic;
using System.Linq;
using Meta.XR.MRUtilityKit;
using Phantom.Environment.Scripts;
using PhantoUtils;
using PhantoUtils.VR;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using static NavMeshConstants;

[MetaCodeSample("Phanto")]
[RequireComponent(typeof(MRUKRoom))]
public class SceneQuery : MonoBehaviour
{
    private static readonly string[] Boundaries = new[] { MRUKAnchor.SceneLabels.WALL_FACE, MRUKAnchor.SceneLabels.INVISIBLE_WALL_FACE, MRUKAnchor.SceneLabels.FLOOR, MRUKAnchor.SceneLabels.CEILING }.Select(e => e.ToString()).ToArray();
    public static readonly string[] Openings = new[] { MRUKAnchor.SceneLabels.DOOR_FRAME, MRUKAnchor.SceneLabels.WINDOW_FRAME, MRUKAnchor.SceneLabels.INVISIBLE_WALL_FACE }.Select(e => e.ToString()).ToArray();

    private static readonly Vector3[] PathCorners = new Vector3[1024];

    // Scene anchors have transform.forward as their "up" direction.
    private static readonly Vector3 AnchorUp = Vector3.forward;

    private static readonly Dictionary<MRUKRoom, SceneQuery> SceneQueries =
        new Dictionary<MRUKRoom, SceneQuery>();

    private Transform _roomTransform;
    private MRUKRoom _room;

    private readonly SpatialHash<MRUKAnchor> _spatialHash = new(0.05f);

    private readonly List<MRUKAnchor> _roomFurnishings = new();

    private readonly Dictionary<Transform, MRUKAnchor> _sceneVolumes = new();
    private readonly Dictionary<Transform, MRUKAnchor> _scenePlanes = new();

    private readonly Dictionary<Transform, MRUKAnchor> _semanticClassifications = new();

    private readonly List<(Transform, MRUKAnchor, float)>
        _candidates = new List<(Transform, MRUKAnchor, float)>();

    private readonly List<PhantoAnchorInfo> _anchorInfos = new List<PhantoAnchorInfo>();

    private bool _ready = false;

    private void OnEnable()
    {
        TryGetComponent(out _room);
        Register();
    }

    private void OnDisable()
    {
        Register(false);
    }

    private void Register(bool register = true)
    {
        Assert.IsNotNull(_room);

        if (register)
        {
            SceneQueries.TryAdd(_room, this);
        }
        else
        {
            SceneQueries.Remove(_room);
        }
    }

    public void Initialize()
    {
        if (_ready)
        {
            Debug.LogWarning($"Attempted to initialize twice: {name}", this);
            return;
        }
        Initialize(GetComponentsInChildren<MRUKAnchor>(true));
    }

    private void Initialize(IEnumerable<MRUKAnchor> semanticClassifications)
    {
        TryGetComponent(out _room);
        _roomTransform = _room.transform;

        _semanticClassifications.Clear();
        _sceneVolumes.Clear();
        _scenePlanes.Clear();
        _roomFurnishings.Clear();
        _anchorInfos.Clear();

        foreach (var sc in semanticClassifications)
        {
            if (sc.GlobalMesh != null)
            {
                Debug.Log("Skipping Scene Mesh in room furnishings");
                continue;
            }
            if (!sc.TryGetComponent(out PhantoAnchorInfo anchorInfo))
            {
                anchorInfo = sc.gameObject.AddComponent<PhantoAnchorInfo>();
            }

            _anchorInfos.Add(anchorInfo);

            var tform = sc.transform;
            var boundary = sc.ContainsAny(Boundaries);

            _semanticClassifications.Add(tform, sc);

            if (sc.TryGetComponent<MRUKAnchor>(out var volume))
            {
                _sceneVolumes.TryAdd(tform, volume);
                if (!boundary)
                {
                    _roomFurnishings.Add(volume);
                }
            }
            else if (sc.TryGetComponent<MRUKAnchor>(out var plane))
            {
                _scenePlanes.TryAdd(tform, plane);
                if (!boundary)
                {
                    _roomFurnishings.Add(volume);
                }
            }
        }

        Register();
        _ready = true;
    }

    public static bool TryGetClosestSemanticClassification(Vector3 worldPoint, Vector3 normal,
        out MRUKAnchor result)
    {
        var closest = float.MaxValue;
        result = null;

        // Check each registered room to find the closest semantic object.
        foreach (var sceneQuery in SceneQueries.Values)
        {
            var (current, distance) = sceneQuery.GetClosestSemanticClassificationInternal(worldPoint, normal);

            if (distance < closest && current != null)
            {
                result = current;
                closest = distance;
            }
        }

        return result != null;
    }

    private (MRUKAnchor, float) GetClosestSemanticClassificationInternal(Vector3 worldPoint,
        Vector3 normal)
    {
        float distance;

        var roomSpacePoint = _roomTransform.InverseTransformPoint(worldPoint);

        if (!_spatialHash.TryGetCell(roomSpacePoint, out var classifications))
        {
            MRUKAnchor result = null;
            (result, distance) = BruteForceSearch(worldPoint, normal);

            if (result != null)
            {
                _spatialHash.Add(roomSpacePoint, result);
            }

            return (result, distance);
        }

        // just return the first item in the spatial hash.
        foreach (var semanticClassification in classifications)
        {
            var tForm = semanticClassification.transform;
            var objectPoint = tForm.InverseTransformPoint(worldPoint);

            // determine distance from point to object.
            distance = Mathf.Abs(new Plane(AnchorUp, Vector3.zero).GetDistanceToPoint(objectPoint));

            return (semanticClassification, distance);
        }

        return (null, float.MaxValue);
    }

    private (MRUKAnchor, float) BruteForceSearch(Vector3 worldPoint, Vector3 normal)
    {
        var minDistance = float.MaxValue;
        MRUKAnchor result = null;
        MonoBehaviour sceneElement = null;

        // iterate through the scene objects to find which "top" is closest to the point.
        foreach (var (tForm, sceneVolume) in _sceneVolumes)
        {
            if (tForm == null)
            {
                // FIXME: Need to prune null entries.
                continue;
            }

            var objectPoint = tForm.InverseTransformPoint(worldPoint);
            var objectNormal = tForm.InverseTransformDirection(normal);

            var objectRay = new Ray(objectPoint, -objectNormal);

            // Does ray intersect the volume?
            if (!InBounds(objectRay, sceneVolume, out var distance))
            {
                continue;
            }

            if (distance < minDistance)
            {
                minDistance = distance;
                result = GetClassification(tForm);
                sceneElement = sceneVolume;
            }
        }

        _candidates.Clear();
        foreach (var (tForm, scenePlane) in _scenePlanes)
        {
            var objectPoint = tForm.InverseTransformPoint(worldPoint);
            var objectNormal = tForm.InverseTransformDirection(normal);

            var objectRay = new Ray(objectPoint, -objectNormal);

            // Does ray intersect the plane?
            if (!InBounds(objectRay, scenePlane, out var distance))
            {
                continue;
            }

            _candidates.Add((tForm, scenePlane, distance));
        }

        var smallestArea = float.MaxValue;

        // since walls and doors, windows, wall art are coplanar with a wall,
        // they will have very similar distance to plane values.
        // prefer returning wall decoration instead of walls.
        for (var i = 0; i < _candidates.Count; i++)
        {
            var (tForm, scenePlane, distance) = _candidates[i];

            var delta = Mathf.Abs(distance - minDistance);
            var area = scenePlane.PlaneRect.Value.width * scenePlane.PlaneRect.Value.height;

            if (sceneElement is MRUKAnchor)
            {
                // if we're comparing two planes that are nearly the same distance from point
                // prefer the smaller plane (e.g. door instead of wall).
                if (delta > 0.02f || area > smallestArea)
                {
                    continue;
                }
            }
            else if (distance > minDistance)
            {
                continue;
            }

            minDistance = distance;
            result = GetClassification(tForm);
            sceneElement = scenePlane;
            smallestArea = area;
        }

        return (result, minDistance);
    }

    private static bool InBounds(Ray ray, MRUKAnchor sceneAnchor, out float distance)
    {
        if (sceneAnchor.PlaneRect.HasValue)
        {
            var plane = new Plane(AnchorUp, Vector3.zero);
            plane.Raycast(ray, out distance);

            if (distance == 0.0f)
            {
                distance = float.MaxValue;
                return false;
            }

            // point where ray intersects plane
            var point = ray.GetPoint(distance);

            var halfDimensions = sceneAnchor.PlaneRect.Value.size / 2;

            // large negative distance means plane on other side of room
            // make sure point is inside bounds of MRUKAnchor.
            if (distance < -0.5f || Mathf.Abs(point.x) > halfDimensions.x || Mathf.Abs(point.y) > halfDimensions.y)
            {
                distance = float.MaxValue;
                return false;
            }

            distance = Mathf.Abs(distance);
            return true;
        }
        else
        {
            var dimensions = sceneAnchor.VolumeBounds.Value.size;

            // test if point is inside the volume.
            var bounds = new Bounds(Vector3.zero, dimensions);

            if (bounds.Contains(ray.origin))
            {
                distance = 0f;
                return true;
            }

            var result = bounds.IntersectRay(ray, out distance);

            if (distance == 0.0f)
            {
                distance = float.MaxValue;
                return false;
            }

            distance = Mathf.Abs(distance);
            return result;
        }

    }

    private MRUKAnchor GetClassification(Transform tForm)
    {
        _semanticClassifications.TryGetValue(tForm, out var semanticClassification);

        return semanticClassification;
    }

    public static bool TryGetVolume(MRUKAnchor classification, out MRUKAnchor volume)
    {
        var xform = classification.transform;

        foreach (var sceneQuery in SceneQueries.Values)
        {
            if (sceneQuery._sceneVolumes.TryGetValue(xform, out volume))
            {
                return true;
            }
        }

        volume = null;
        return false;
    }

    public static bool TryGetPlane(MRUKAnchor classification, out MRUKAnchor plane)
    {
        var xform = classification.transform;

        foreach (var sceneQuery in SceneQueries.Values)
        {
            if (sceneQuery._scenePlanes.TryGetValue(xform, out plane))
            {
                return true;
            }
        }

        plane = null;
        return false;
    }

    public static bool TryGetSqrPathLength(Vector3 point, Vector3 destination, out float length)
    {
        length = 0f;
        var path = new NavMeshPath();

        // calculate path from vertex to crystal.
        if (!NavMesh.CalculatePath(point, destination, NavMesh.AllAreas, path))
        {
            return false;
        }

        var cornerCount = path.GetCornersNonAlloc(PathCorners);

        // sum the length of the path
        for (var i = 1; i < cornerCount; i++)
        {
            length += (PathCorners[i - 1] - PathCorners[i]).sqrMagnitude;
        }

        return true;
    }

    public static bool VerifyPointIsOpen(Vector3 point, Plane ceilingPlane, int layerMask = -1)
    {
        // FIXME: don't pass in the ceiling plane and mask. determine room from point and ceiling from room.

        if (layerMask == -1)
        {
            layerMask = SceneMeshLayerMask;
        }

        var pointOnCeiling = ceilingPlane.ClosestPointOnPlane(point);

        if (!Physics.SphereCast(pointOnCeiling, TennisBall, Vector3.down, out var raycastHit, 1000.0f,
                layerMask,
                QueryTriggerInteraction.Ignore))
        {
#if VERBOSE_DEBUG
                Debug.Log(
                    $"point: {pointOnCeiling} raycast: {raycastHit.point.ToString("F2")} dist: {raycastHit.distance}");
#endif

            return false;
        }

        return Vector3.Distance(point, raycastHit.point) <= TennisBall * 2.0f;
    }

    public static bool TryGetRandomFurniture(out FurnitureNavMeshGenerator furniture)
    {
        if (FurnitureNavMeshGenerator.FurnitureNavMesh.Count == 0)
        {
            furniture = null;
            return false;
        }

        furniture = FurnitureNavMeshGenerator.FurnitureNavMesh.RandomElement();
        Assert.IsNotNull(furniture);

        return true;
    }

    public static int GetFurnitureWithClassifications(IReadOnlyList<string> classifications,
        List<FurnitureNavMeshGenerator> results)
    {
        results.Clear();

        foreach (var furniture in FurnitureNavMeshGenerator.FurnitureNavMesh)
        {
            if (furniture.HasNavMesh && furniture.Classification.ContainsAny(classifications))
            {
                results.Add(furniture);
            }
        }

        return results.Count;
    }

    public static bool TryGetFurniture(IReadOnlyList<string> classifications, out FurnitureNavMeshGenerator result)
    {
        var results = new List<FurnitureNavMeshGenerator>();

        var count = GetFurnitureWithClassifications(classifications, results);

        if (count == 0)
        {
            result = null;
            return false;
        }

        result = results.RandomElement();
        return true;
    }

    public static Vector3 RandomPointOnFurniture()
    {
        NavMeshTriangle randomTriangle;

        if (TryGetRandomFurniture(out var furniture) && furniture.HasNavMesh)
        {
            return furniture.RandomPoint();
        }

        var navMeshGenerator = GetNavMeshGenerator();

        randomTriangle = navMeshGenerator.RandomFloorTriangle();
        return randomTriangle.GetRandomPoint();
    }

    private static NavMeshGenerator GetNavMeshGenerator()
    {
        NavMeshGenerator result = null;

        foreach (var room in SceneQueries.Keys)
        {
            if (NavMeshGenerator.TryGetNavMeshGenerator(room, out result))
            {
                break;
            }
        }

        return result;
    }

    public static bool TryGetClosestPoint(Vector3 position, out Vector3 point, float maxDistance)
    {
        if (!NavMesh.SamplePosition(position, out var navMeshHit, maxDistance, NavMesh.AllAreas))
        {
            point = default;
            return false;
        }

        // find which triangle the point belongs to.
        point = navMeshHit.position;
        return true;
    }

    // public static NavMeshTriangle ClosestFloorTriangleOnCircle(Vector3 point, float radius)
    // {
    //     if (NavMeshBookKeeper.TryGetClosestTriangleOnCircle(point, radius, FloorNavMeshSurface, out var result))
    //         return result;
    //
    //     Debug.LogWarning("Finding floor navmesh triangle failed.");
    //     return default;
    // }

    public static bool TryGetClosestPointOnNavMesh(ref Vector3 point, int areaMask = NavMesh.AllAreas,
        float maxDistance = 10.0f)
    {
        // snap point to navmesh
        if (!NavMesh.SamplePosition(point, out var navMeshHit, maxDistance, areaMask)) return false;

        point = navMeshHit.position;
        return true;
    }

    public static Vector3 RandomPointOnFloor(Vector3 position, float minDistance, bool verifyOpenArea = true)
    {
        var navMeshGenerator = GetNavMeshGenerator();

        return navMeshGenerator.RandomPointOnFloor(position, minDistance, verifyOpenArea);
    }

    public static Vector3 RandomPointOnFloorNearUser(float minDistance = 1.0f, bool verifyOpenArea = true)
    {
        var position = CameraRig.Instance.CenterEyeAnchor.position;

        var navMeshGenerator = GetNavMeshGenerator();

        return navMeshGenerator.RandomPointOnFloor(position, minDistance, verifyOpenArea);
    }

    public static Vector3 RandomPointOnFurniture(Vector3 position, float minDistance)
    {
        return RandomPointOnFurniture();
    }

    /// <summary>
    /// Get a plane that represents the highest point in the room.
    /// It will either be ceiling plane or a plane one foot above tallest piece of furniture.
    /// </summary>
    /// <param name="room"></param>
    /// <returns></returns>
    public static Plane GetLid(MRUKRoom room)
    {
        var floorPos = room.FloorAnchor.transform.position;
        var ceilingPos = room.CeilingAnchor.transform.position;

        var lid = new Plane(Vector3.down, ceilingPos);

        if (!SceneQueries.TryGetValue(room, out var sceneQuery))
        {
            return lid;
        }

        var floorPlane = new Plane(Vector3.up, floorPos);

        var maxHeight = float.MinValue;

        foreach (MRUKAnchor furniture in sceneQuery._roomFurnishings)
        {
            if (furniture == null)
            {
                continue;
            }

            float height = default;

            var furnitureTransform = furniture.transform;

            if (furniture.VolumeBounds.HasValue == false && furniture.PlaneBoundary2D == null)
            {
                Debug.Log($"Skipping {furniture.name} since it does not have boundary or volume");
                continue;
            }

            if (furniture.VolumeBounds.HasValue)
            {
                height = floorPlane.GetDistanceToPoint(furnitureTransform.position);
            }
            else
            {
                height = 0;
                foreach (var point in furniture.PlaneBoundary2D)
                {
                    var worldPoint = furnitureTransform.TransformPoint(point);
                    height = Mathf.Max(height, floorPlane.GetDistanceToPoint(worldPoint));
                }
            }

            if (height > maxHeight)
            {
                maxHeight = height;
            }
        }

        maxHeight += OneFoot;
        var ceilingHeight = floorPlane.GetDistanceToPoint(ceilingPos);

        if (ceilingHeight < maxHeight)
        {
            return lid;
        }

        maxHeight += floorPos.y;
        lid.distance = maxHeight;

        return lid;
    }

    /// <summary>
    /// Search "other room" for an opening that is the mirror
    /// of the source opening.
    /// </summary>
    /// <param name="opening"></param>
    /// <param name="otherRoom"></param>
    /// <param name="sibling"></param>
    /// <returns></returns>
    public static bool TryGetLinkedOpening(PhantoAnchorInfo sourceOpening, MRUKRoom otherRoom,
        out PhantoAnchorInfo sibling)
    {
        if (!SceneQueries.TryGetValue(otherRoom, out var sceneQuery))
        {
            sibling = null;
            return false;
        }

        var openings = new List<PhantoAnchorInfo>(sceneQuery._anchorInfos);
        openings.RemoveAll((classification) => !classification.ContainsAny(Openings));

        foreach (var opening in openings)
        {
            var dot = Vector3.Dot(sourceOpening.Forward, opening.Forward);

            var openingPos = opening.Position;

            // the two openings face away from each other
            // a ray from opening passes through source opening
            if (dot > -0.9f || !sourceOpening.PlaneContainsPoint(openingPos, out var distance))
            {
                continue;
            }

            distance = Mathf.Abs(distance);
            var openingWidth = Mathf.Max(sourceOpening.Width, opening.Width);

            // distance between openings is less than half the width.
            // (how thick are walls?)
            if (distance < openingWidth * 0.5f)
            {
                sibling = opening;
                return true;
            }
        }

        sibling = null;
        return false;
    }

    /// <summary>
    /// Returns room the contains or is closest to provided point
    /// </summary>
    /// <param name="point">world space point</param>
    /// <returns></returns>
    public static MRUKRoom GetRoomContainingPoint(Vector3 point)
    {
        var minDistance = float.MaxValue;
        MRUKRoom result = null;

        foreach (var room in SceneQueries.Keys)
        {
            var distance = DistanceFromRoom(room, point);

            if (distance < minDistance)
            {
                minDistance = distance;
                result = room;
            }
        }

        return result;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="point"></param>
    /// <param name="result"></param>
    /// <returns>true if this point is within a room</returns>
    public static bool TryGetRoomContainingPoint(Vector3 point, out MRUKRoom result)
    {
        foreach (var room in SceneQueries.Keys)
        {
            var floorTransform = room.FloorAnchor.transform;
            var floorPoint = Vector3.ProjectOnPlane(floorTransform.InverseTransformPoint(point), Vector3.forward);

            if (PointInPolygon2D(room.FloorAnchor.PlaneBoundary2D, floorPoint))
            {
                result = room;
                return true;
            }
        }

        result = GetRoomContainingPoint(point);
        return false;
    }

    /// <summary>
    /// Determine if this opening (door, window, etc) connects
    /// two adjacent rooms. Provides points to create a navmesh link between the rooms.
    /// </summary>
    /// <param name="room">room the opening belongs to</param>
    /// <param name="opening">potential route to another room</param>
    /// <param name="front">point on the floor in front of the opening</param>
    /// <param name="back">point on the floor behind the opening</param>
    /// <param name="otherRoom">room this opening connects to</param>
    /// <returns></returns>
    public static bool TryGetConnectingRoom(MRUKRoom room, PhantoAnchorInfo opening, out Vector3 front,
        out Vector3 back,
        out MRUKRoom otherRoom)
    {
        otherRoom = null;
        back = default;
        var probeLength = Mathf.Max(opening.Width * 0.5f, 0.4f);
        var planeTransform = opening.transform;

        var ray = new Ray(planeTransform.position, planeTransform.forward);
        front = ray.GetPoint(probeLength);

        // find room probeLength in front of door.
        // var frontRoom = RoomContainingPoint(front, true);

        // if either is null, return false.
        if (!TryGetRoomContainingPoint(front, out var frontRoom))
        {
            return false;
        }

        if (frontRoom != room)
        {
            otherRoom = frontRoom;
        }

        // find room probeLength behind door.
        back = ray.GetPoint(-probeLength);
        // var backRoom = RoomContainingPoint(back, true);

        // if they're the same room, return false.
        if (!TryGetRoomContainingPoint(back, out var backRoom) || frontRoom == backRoom)
        {
            return false;
        }

        if (backRoom != room)
        {
            otherRoom = backRoom;
        }

        // otherwise set front and back to positions on floor for navmesh link.
        Debug.LogError("We reached here, and we should not");
        return true;
    }

    private static float DistanceFromRoom(MRUKRoom room, Vector3 point)
    {
        var roomTransform = room.transform;
        var floorTransform = room.FloorAnchor.transform;

        var localSpacePoint = roomTransform.InverseTransformPoint(point);

        // local space bounding box for room.
        var roomBounds = new Bounds(Vector3.zero, Vector3.zero);

        roomBounds.Encapsulate(room.CeilingAnchor.transform.localPosition);
        roomBounds.Encapsulate(floorTransform.localPosition);

        foreach (var wall in room.WallAnchors)
        {
            roomBounds.Encapsulate(wall.transform.localPosition);
        }

        if (roomBounds.Contains(localSpacePoint))
        {
            var floorPoint = Vector3.ProjectOnPlane(floorTransform.InverseTransformPoint(point), Vector3.forward);

            if (PointInPolygon2D(room.FloorAnchor.PlaneBoundary2D, floorPoint))
            {
                return 0;
            }
        }

        return Mathf.Sqrt(roomBounds.SqrDistance(localSpacePoint));
    }

    /// <summary>
    /// Determines if a point is inside of a 2d polygon.
    /// </summary>
    /// <param name="boundaryVertices">The vertices that make up the bounds of the polygon</param>
    /// <param name="target">The target point to test</param>
    /// <returns>True if the point is inside the polygon, false otherwise</returns>
    public static bool PointInPolygon2D(IReadOnlyList<Vector2> boundaryVertices, Vector2 target)
    {
        var count = boundaryVertices.Count;

        if (count < 3)
            return false;

        int collision = 0;
        var x = target.x;
        var y = target.y;

        for (int i = 0; i < count; i++)
        {
            var x1 = boundaryVertices[i].x;
            var y1 = boundaryVertices[i].y;

            var x2 = boundaryVertices[(i + 1) % count].x;
            var y2 = boundaryVertices[(i + 1) % count].y;

            if (y < y1 != y < y2 &&
                x < x1 + ((y - y1) / (y2 - y1)) * (x2 - x1))
            {
                collision += (y1 < y2) ? 1 : -1;
            }
        }

        return collision != 0;
    }

    public static Bounds GetRoomBounds(MRUKRoom room)
    {
        var floor = room.FloorAnchor;
        var ceiling = room.CeilingAnchor;
        Assert.IsNotNull(floor);
        Assert.IsNotNull(ceiling);

        var floorTransform = floor.transform;
        var ceilingTransform = ceiling.transform;

        var bounds = new Bounds(floorTransform.position, Vector3.zero);

        foreach (var point in floor.PlaneBoundary2D)
        {
            bounds.Encapsulate(floorTransform.TransformPoint(point));
        }

        foreach (var point in ceiling.PlaneBoundary2D)
        {
            bounds.Encapsulate(ceilingTransform.TransformPoint(point));
        }

        return bounds;
    }

    /// <summary>
    /// Get a bounding box that encompasses all loaded rooms.
    /// This can be used to determine if something has escaped.
    /// </summary>
    /// <returns></returns>
    public static Bounds GetWorldBounds()
    {
        Bounds? worldBounds = null;

        foreach (var room in SceneQueries.Keys)
        {
            if (!worldBounds.HasValue)
            {
                worldBounds = GetRoomBounds(room);
            }
            else
            {
                var temp = worldBounds.Value;
                temp.Encapsulate(GetRoomBounds(room));

                worldBounds = temp;
            }
        }

        return worldBounds.GetValueOrDefault();
    }
}
