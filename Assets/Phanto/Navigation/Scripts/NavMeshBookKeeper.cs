// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

/// <summary>
///     Utility to keep track of which triangles in the NavMesh
///     are associated with a specific surface.
/// </summary>
public static class NavMeshBookKeeper
{
    private static List<NavMeshTriangle> _triangles;

    private static readonly Dictionary<NavMeshSurface, List<NavMeshTriangle>> _meshOwners = new();

    public static event Action OnValidateScene;

    /// <summary>
    ///     Instead of calling NavMeshSurface.BuildNavMesh()
    ///     use this method so we know which triangles belong to which surface.
    /// </summary>
    /// <param name="owner"></param>
    public static List<NavMeshTriangle> GenerateNavMeshTriangles(this NavMeshSurface owner)
    {
        if (_meshOwners.ContainsKey(owner))
        {
            // Remove the old triangles from the list
            owner.RemoveData();
            _triangles = CalculateTriangles();
        }

        owner.BuildNavMesh();

        var newTriangles = CalculateTriangles();

        if (_triangles == null)
        {
            _meshOwners[owner] = newTriangles;
            _triangles = new List<NavMeshTriangle>(newTriangles);
            return newTriangles;
        }

        // Rigure out which triangles in the array are new. move them to a new triangle collection
        var newCount = newTriangles.Count;
        var oldCount = _triangles.Count;

        // Number of new triangles we're looking for.
        var delta = newCount - oldCount;

        Assert.IsFalse(delta < 0);

        if (delta == 0)
        {
            var empty = new List<NavMeshTriangle>();
            _meshOwners[owner] = empty;
            return empty;
        }

        var ownedTriangles = new List<NavMeshTriangle>(delta);

        // Go through the triangles to find new ones (slow-ish).
        for (var i = 0; i < newCount; i++)
        {
            var current = newTriangles[i];
            var found = false;

            for (var j = 0; j < oldCount; j++)
            {
                if (_triangles[j].Equals(current))
                {
                    found = true;
                    break;
                }
            }

            if (!found) ownedTriangles.Add(current);
        }

        _meshOwners[owner] = ownedTriangles;
        _triangles = newTriangles;
        return ownedTriangles;
    }

    /// <summary>
    ///     Clean up when a NavMeshSurface is about to be destroyed.
    /// </summary>
    /// <param name="owner"></param>
    /// <returns></returns>
    public static bool ClearNavMeshTriangles(this NavMeshSurface owner)
    {
        if (_meshOwners.Remove(owner))
        {
            owner.RemoveData();
            _triangles = CalculateTriangles();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Spherecast from the ceiling down to the triangle to verify it's in an "open" area.
    /// </summary>
    /// <param name="room"></param>
    public static IEnumerator ValidateScene(OVRSceneRoom room)
    {
        var stopwatch = Stopwatch.StartNew();

        var ceilingTransform = room.Ceiling.transform;
        var ceilingPlane = new Plane(ceilingTransform.forward, ceilingTransform.position);

        // verify each triangle is not under furniture.
        foreach (var triangleList in _meshOwners.Values)
        {
            if (triangleList == null)
            {
                continue;
            }

            foreach (var triangle in triangleList)
            {
                triangle.VerifyOpen(ceilingPlane);
            }

            if (stopwatch.ElapsedMilliseconds > 5)
            {
                Debug.Log($"validating scene: {stopwatch.Elapsed}");
                yield return null;
                stopwatch.Restart();
            }
        }

        OnValidateScene?.Invoke();
    }

    private static List<NavMeshTriangle> CalculateTriangles()
    {
        var edgeCounts = new Dictionary<NavMeshTriangle.Edge, int>();

        var triangulation = NavMesh.CalculateTriangulation();

        var indices = triangulation.indices;
        var verts = triangulation.vertices;
        var areas = triangulation.areas;

        var length = indices.Length;
        var triangles = new List<NavMeshTriangle>(length / 3);

        var a = 0;
        for (var i = 0; i < length; i += 3)
        {
            var (i1, i2, i3) = (indices[i], indices[i + 1], indices[i + 2]);

            var triangle = new NavMeshTriangle(verts[i1], verts[i2], verts[i3], areas[a]);

            triangle.IncrementEdgeCounts(edgeCounts);

            triangles.Add(triangle);
            a++;
        }

        foreach (var triangle in triangles) triangle.DetermineEdges(edgeCounts);

        return triangles;
    }

    public static List<NavMeshTriangle> GetTriangles(NavMeshSurface owner)
    {
        if (_meshOwners.TryGetValue(owner, out var tris)) return tris;

        return null;
    }

    public static List<NavMeshTriangle> GetAllTriangles()
    {
        var result = new List<NavMeshTriangle>();

        foreach (var list in _meshOwners.Values)
        foreach (var tri in list)
        {
            if (!tri.IsOpen) continue;

            result.Add(tri);
        }

        return result;
    }

    public static List<NavMeshTriangle> GetTrianglesWithId(int areaId, bool isOpen = true)
    {
        var result = new List<NavMeshTriangle>();
        var openCount = 0;

        foreach (var triangle in _triangles)
        {
            if (triangle.areaId == areaId)
            {
                result.Add(triangle);
                // we don't have a guarantee that any triangles will be open.
                if (triangle.IsOpen)
                {
                    openCount++;
                }
            }
        }

        // open triangles means they have obstructed view of the ceiling.
        // they should be preferred for most kinds of spawning.
        if (openCount > 0 && isOpen)
        {
            result.RemoveAll((x) => !x.IsOpen);
        }

        Assert.IsTrue(result.Count != 0);

        return result;
    }

    public static bool TryGetClosestTriangleOnCircle(Vector3 point, float radius, NavMeshSurface owner,
        out NavMeshTriangle triangle)
    {
        if (!_meshOwners.TryGetValue(owner, out var triangles))
        {
            triangle = default;
            return false;
        }

        if (triangles == null)
        {
            Debug.LogError("Triangles list is null!", owner);
            triangle = default;
            return false;
        }

        var result = triangles[0];
        var plane = new Plane(Vector3.up, result.center);

        // project point onto floor plane.
        point = plane.ClosestPointOnPlane(point);

        var minDistance = float.MaxValue;

        foreach (var tri in triangles)
        {
            var planeCenter = plane.ClosestPointOnPlane(tri.center);

            var distance = Vector3.Distance(planeCenter, point);

            // inside the circle
            if (distance < radius) distance = radius - distance;

            if (distance < minDistance)
            {
                minDistance = distance;
                result = tri;
            }
        }

        triangle = result;
        return true;
    }

    public static NavMeshTriangle ClosestTriangleOnCircle(Vector3 point, float radius, bool isOpen = true)
    {
        Assert.IsNotNull(_triangles);

        if (_triangles == null)
        {
            Debug.LogError($"[{nameof(NavMeshGenerator)}] called before navmesh has been generated.");
            return default;
        }

        Plane plane;
        NavMeshTriangle result = _triangles[0];

        if (isOpen)
        {
            // find the first open triangle
            for (var i = 0; i < _triangles.Count; i++)
            {
                if (_triangles[i].IsOpen)
                {
                    result = _triangles[i];
                    break;
                }
            }
        }

        if (!NavMesh.SamplePosition(point, out var navMeshHit, 100.0f, NavMesh.AllAreas))
        {
            Debug.LogWarning("Point is not near navmesh.");
            plane = new Plane(Vector3.up, result.center);
        }
        else
        {
            point = navMeshHit.position;
            plane = new Plane(Vector3.up, point);
        }

        var minDistance = float.MaxValue;

        foreach (var ownedTriangles in _meshOwners.Values)
        {
            foreach (var tri in ownedTriangles)
            {
                if (isOpen && !tri.IsOpen)
                {
                    continue;
                }

                var planeCenter = plane.ClosestPointOnPlane(tri.center);

                var distance = Vector3.Distance(planeCenter, point);

                // inside the circle
                if (distance < radius) distance = radius - distance;

                if (distance < minDistance)
                {
                    minDistance = distance;
                    result = tri;
                }
            }
        }

        return result;
    }

    public static int TrianglesInRadius(Vector3 point, float radius, bool isOpen, List<NavMeshTriangle> results)
    {
        results.Clear();

        var plane = new Plane(Vector3.up, point);
        var sqrRadius = radius * radius;

        foreach (var ownedTriangles in _meshOwners.Values)
        {
            foreach (var tri in ownedTriangles)
            {
                if (isOpen && !tri.IsOpen)
                {
                    continue;
                }

                // does the triangle straddle the circle?
                var count = 0;
                count += InsideRadius(tri.v1, point, sqrRadius, ref plane) ? -1 : 1;
                count += InsideRadius(tri.v2, point, sqrRadius, ref plane) ? -1 : 1;
                count += InsideRadius(tri.v3, point, sqrRadius, ref plane) ? -1 : 1;

                // if count is 3 the triangle is fully outside the circle.
                if (count < 3)
                {
                    results.Add(tri);
                }
            }
        }

        return results.Count;

    }

    public static int TrianglesOnCircle(Vector3 point, float radius, bool isOpen, List<NavMeshTriangle> results)
    {
        results.Clear();

        var plane = new Plane(Vector3.up, point);
        var sqrRadius = radius * radius;

        foreach (var ownedTriangles in _meshOwners.Values)
        {
            foreach (var tri in ownedTriangles)
            {
                if (isOpen && !tri.IsOpen)
                {
                    continue;
                }

                // does the triangle straddle the circle?
                var count = 0;
                count += InsideRadius(tri.v1, point, sqrRadius, ref plane) ? -1 : 1;
                count += InsideRadius(tri.v2, point, sqrRadius, ref plane) ? -1 : 1;
                count += InsideRadius(tri.v3, point, sqrRadius, ref plane) ? -1 : 1;

                // if count is -3 the triangle is fully inside the circle.
                // if count is 3 the triangle is fully outside the circle.
                if (count < 3 && count > -3)
                {
                    results.Add(tri);
                }
            }
        }

        return results.Count;
    }

    public static bool FindMatchingPoint(List<NavMeshTriangle> triangles, out Vector3 result, Func<Vector3, NavMeshTriangle, bool> matchCondition)
    {
        // Make multiple attempts to find a location that's "padding" centimeters away from an edge.
        result = default;

        for (int i = 0; i < 100; i++)
        {
            for (var j = 0; j < triangles.Count; j++)
            {
                var tri = triangles[j];
                result = tri.GetRandomPoint();

                if (matchCondition(result, tri))
                {
                    return true;
                }
            }
        }

        return false;
    }


    private static bool InsideRadius(Vector3 point, Vector3 circleCenter, float circleSqrRadius, ref Plane circlePlane)
    {
        // we don't want vertical distance in this calculation
        // project the point onto the circle's plane.
        var projectedPoint = circlePlane.ClosestPointOnPlane(point);

        var sqrMag = (circleCenter - projectedPoint).sqrMagnitude;

        return sqrMag <= circleSqrRadius;
    }
}
