// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using PhantoUtils;
using PhantoUtils.VR;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using static NavMeshConstants;
using static OVRSceneManager.Classification;

[RequireComponent(typeof(OVRSceneRoom))]
public class SceneQuery : MonoBehaviour
{
    private static readonly string[] Boundaries = new[] { WallFace, InvisibleWallFace, Floor, Ceiling };

    private static readonly Vector3[] PathCorners = new Vector3[1024];

    // Scene anchors have transform.forward as their "up" direction.
    private static readonly Vector3 AnchorUp = Vector3.forward;
    private static readonly Dictionary<OVRSceneRoom, SceneQuery> SceneQueries = new Dictionary<OVRSceneRoom, SceneQuery>();

    private Transform _roomTransform;
    private OVRSceneRoom _room;

    private readonly SpatialHash<OVRSemanticClassification> _spatialHash = new(0.05f);

    private readonly List<Component> _roomFurnishings = new();

    private readonly Dictionary<Transform, OVRSceneVolume> _sceneVolumes = new();
    private readonly Dictionary<Transform, OVRScenePlane> _scenePlanes = new();

    private readonly Dictionary<Transform, OVRSemanticClassification> _semanticClassifications = new();

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

    public void Initialize(IReadOnlyCollection<OVRSemanticClassification> semanticClassifications)
    {
        TryGetComponent(out _room);
        _roomTransform = _room.transform;

        _semanticClassifications.Clear();
        _sceneVolumes.Clear();
        _scenePlanes.Clear();
        _roomFurnishings.Clear();

        foreach (var sc in semanticClassifications)
        {
            var tform = sc.transform;
            var boundary = sc.ContainsAny(Boundaries);

            _semanticClassifications.Add(tform, sc);

            if (sc.TryGetComponent<OVRSceneVolume>(out var volume))
            {
                _sceneVolumes.TryAdd(tform, volume);
                if (!boundary)
                {
                    _roomFurnishings.Add(volume);
                }
            }
            else if (sc.TryGetComponent<OVRScenePlane>(out var plane))
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
        out OVRSemanticClassification result)
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

    private (OVRSemanticClassification, float) GetClosestSemanticClassificationInternal(Vector3 worldPoint, Vector3 normal)
    {
        float distance;

        var roomSpacePoint = _roomTransform.InverseTransformPoint(worldPoint);

        if (!_spatialHash.TryGetCell(roomSpacePoint, out var classifications))
        {
            OVRSemanticClassification result = null;
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

    private readonly List<(Transform, OVRScenePlane, float)> _candidates = new List<(Transform, OVRScenePlane, float)>();

    private (OVRSemanticClassification, float) BruteForceSearch(Vector3 worldPoint, Vector3 normal)
    {
        var minDistance = float.MaxValue;
        OVRSemanticClassification result = null;
        MonoBehaviour sceneElement = null;

        // iterate through the scene objects to find which "top" is closest to the point.
        foreach (var (tForm, sceneVolume) in _sceneVolumes)
        {
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
            var area = scenePlane.Height * scenePlane.Width;

            if (sceneElement is OVRScenePlane)
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

    private static bool InBounds(Ray ray, OVRScenePlane scenePlane, out float distance)
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

        var halfDimensions = scenePlane.Dimensions / 2;

        // large negative distance means plane on other side of room
        // make sure point is inside bounds of OVRScenePlane.
        if (distance < -0.5f || Mathf.Abs(point.x) > halfDimensions.x || Mathf.Abs(point.y) > halfDimensions.y)
        {
            distance = float.MaxValue;
            return false;
        }

        distance = Mathf.Abs(distance);
        return true;
    }

    private static bool InBounds(Ray ray, OVRSceneVolume sceneVolume, out float distance)
    {
        var dimensions = sceneVolume.Dimensions;

        // test if point is inside the volume.
        var bounds = new Bounds(Vector3.zero, dimensions);
        var offset = sceneVolume.Offset;
        offset.z -= bounds.extents.z;
        bounds.center = offset;

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

    private OVRSemanticClassification GetClassification(Transform tForm)
    {
        _semanticClassifications.TryGetValue(tForm, out var semanticClassification);

        return semanticClassification;
    }

    public static bool TryGetVolume(OVRSemanticClassification classification, out OVRSceneVolume volume)
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

    public static bool TryGetPlane(OVRSemanticClassification classification, out OVRScenePlane plane)
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
    public static Plane GetLid(OVRSceneRoom room)
    {
        var floorPos = room.Floor.transform.position;
        var ceilingPos = room.Ceiling.transform.position;

        var lid = new Plane(Vector3.down, ceilingPos);

        if (!SceneQueries.TryGetValue(room, out var sceneQuery))
        {
            return lid;
        }

        var floorPlane = new Plane(Vector3.up, floorPos);

        var maxHeight = float.MinValue;

        foreach (var furniture in sceneQuery._roomFurnishings)
        {
            if (furniture == null)
            {
                continue;
            }

            float height = default;

            var furnitureTransform = furniture.transform;

            switch (furniture)
            {
                case OVRSceneVolume volume:
                    height = floorPlane.GetDistanceToPoint(furnitureTransform.position);
                    break;
                case OVRScenePlane plane:
                    height = 0;
                    foreach (var point in plane.Boundary)
                    {
                        var worldPoint = furnitureTransform.TransformPoint(point);
                        height = Mathf.Max(height, floorPlane.GetDistanceToPoint(worldPoint));
                    }
                    break;
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
}
