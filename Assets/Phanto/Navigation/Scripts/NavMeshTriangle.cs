// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PhantoUtils;
using UnityEngine;
using UnityEngine.AI;
using Utilities.XR;
using static NavMeshConstants;
using Random = UnityEngine.Random;

/// <summary>
/// Represents a triangle in a NavMesh
/// </summary>
public class NavMeshTriangle : IEquatable<NavMeshTriangle>
{
    private readonly bool[] _borderEdges;

    private readonly Edge[] _edges;
    public readonly int areaId;
    public readonly Vector3 center;

    public readonly Vector3 v1;
    public readonly Vector3 v2;
    public readonly Vector3 v3;

    public int AreaMask => 1 << areaId;

    private float _area;

    public NavMeshTriangle(Vector3 va, Vector3 vb, Vector3 vc, int id)
    {
        (v1, v2, v3) = (va, vb, vc);

        center = (v1 + v2 + v3) / 3.0f;
        areaId = id;
        _area = 0.0f;

        _edges = new Edge[]
        {
            new(v1, v2),
            new(v2, v3),
            new(v3, v1)
        };

        _borderEdges = new bool[3];
    }

    public bool IsOpen { get; private set; }

    public bool IsBorder { get; private set; }

    public float Area
    {
        get
        {
            if (_area == 0.0f) _area = CalculateTriangleArea();

            return _area;
        }
    }

    public bool Equals(NavMeshTriangle other)
    {
        return areaId == other.areaId && v1.Approximately(other.v1) && v2.Approximately(other.v2) &&
               v3.Approximately(other.v3);
    }

    private float CalculateTriangleArea()
    {
        // Calculate the lengths of the triangle sides
        var side1 = Vector3.Distance(v1, v2);
        var side2 = Vector3.Distance(v2, v3);
        var side3 = Vector3.Distance(v3, v1);

        // Use Heron's formula to calculate the triangle area
        var semiPerimeter = (side1 + side2 + side3) / 2f;
        var area = Mathf.Sqrt(semiPerimeter * (semiPerimeter - side1) * (semiPerimeter - side2) *
                              (semiPerimeter - side3));

        return area;
    }

    // Formula from https://math.stackexchange.com/a/4023675
    public Vector3 GetRandomPoint()
    {
        var a = Mathf.Sqrt(Random.value);
        var b = Random.value;

        var p = (1 - a) * v1 + a * (1 - b) * v2 + b * a * v3;

        // Since the triangulated navmesh won't accurately capture curvature of surfaces,
        // we need to snap to the actual nav mesh.
        return SnapPointToNavMesh(p);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 SnapPointToNavMesh(Vector3 point, float maxDistance = 1.0f)
    {
        if (!NavMesh.SamplePosition(point, out var meshHit, maxDistance, 1 << areaId))
        {
            Debug.LogWarning($"No navmesh with mask {areaId} near point!");
            return point;
        }

        return meshHit.position;
    }

    public override bool Equals(object obj)
    {
        return obj is NavMeshTriangle other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(v1, v2, v3, areaId);
    }

    public void DrawGizmo()
    {
        Gizmos.DrawLine(v1, v2);
        Gizmos.DrawLine(v2, v3);
        Gizmos.DrawLine(v3, v1);
    }

    public bool VerifyOpen(Plane ceilingPlane)
    {
        // spherecast from ceiling plane to center of triangle
        var ceiling = ceilingPlane.ClosestPointOnPlane(center);

        if (!Physics.SphereCast(ceiling, TennisBall, Vector3.down, out var raycastHit, 1000.0f, SceneMeshLayerMask,
                QueryTriggerInteraction.Ignore))
        {
#if VERBOSE_DEBUG
            Debug.Log($"point: {ceiling} raycast: {raycastHit.point.ToString("F2")} dist: {raycastHit.distance}");
#endif

            IsOpen = false;
            return false;
        }

        IsOpen = Vector3.Distance(center, raycastHit.point) <= TennisBall * 2.0f;

        return IsOpen;
    }

    public void IncrementEdgeCounts(Dictionary<Edge, int> edgeCounts)
    {
        foreach (var edge in _edges)
        {
            var found = false;

            foreach (var (other, count) in edgeCounts)
                if (edge.Equals(other))
                {
                    edgeCounts[other] = count + 1;
                    found = true;
                    break;
                }

            if (!found) edgeCounts[edge] = 1;
        }
    }

    public void DetermineEdges(Dictionary<Edge, int> edgeCounts)
    {
        foreach (var (other, count) in edgeCounts)
            for (var i = 0; i < _edges.Length; i++)
            {
                if (_edges[i].Equals(other) && count == 1)
                {
                    _borderEdges[i] = true;
                    IsBorder = true;
                }
            }
    }

    public void DebugDraw(Color color)
    {
        for (var i = 0; i < 3; i++)
        {
            var edge = _edges[i];

            XRGizmos.DrawLine(edge.a, edge.b, color, 0.006f);
        }
    }

    public void DebugDraw(Color borderColor, Color notOpen)
    {
        if (!IsOpen)
        {
            XRGizmos.DrawPoint(center, Color.red, 0.1f, 0.006f);

            for (var i = 0; i < 3; i++)
            {
                var (edge, borderEdge) = (_edges[i], _borderEdges[i]);

                if (!borderEdge) XRGizmos.DrawLine(edge.a, edge.b, notOpen, 0.006f);
            }
        }

        if (IsBorder)
            for (var i = 0; i < 3; i++)
            {
                var (edge, borderEdge) = (_edges[i], _borderEdges[i]);

                if (borderEdge) XRGizmos.DrawLine(edge.a, edge.b, borderColor, 0.006f);
            }
    }

    /// <summary>
    /// Returns the vertex of the triangle furthest from the provided point
    /// Used to try and find the longest path from A to B.
    /// </summary>
    /// <param name="point"></param>
    /// <param name="tri"></param>
    /// <param name="awayFromEdge">Shift the point 5 cm away from the triangle's corner</param>
    /// <returns></returns>
    public Vector3 FurthestVert(Vector3 point)
    {
        var result = v1;
        var maxDistance = (point - v1).sqrMagnitude;

        var distance = (point - v2).sqrMagnitude;
        if (distance > maxDistance)
        {
            result = v2;
            maxDistance = distance;
        }

        distance = (point - v3).sqrMagnitude;
        if (distance > maxDistance)
        {
            result = v3;
        }

        return result;
    }

    public Vector3 ClosestVert(Vector3 point)
    {
        var result = v1;
        var minDistance = (point - v1).sqrMagnitude;

        var distance = (point - v2).sqrMagnitude;
        if (distance < minDistance)
        {
            result = v2;
            minDistance = distance;
        }

        distance = (point - v3).sqrMagnitude;
        if (distance < minDistance)
        {
            result = v3;
        }

        return result;
    }

    public readonly struct Edge : IEquatable<Edge>
    {
        public readonly Vector3 a;
        public readonly Vector3 b;

        public Edge(Vector3 v1, Vector3 v2)
        {
            a = v1;
            b = v2;
        }

        public bool Equals(Edge other)
        {
            // Handle the case where the points are reversed.
            return (a.Approximately(other.a) && b.Approximately(other.b))
                   || (a.Approximately(other.b) && b.Approximately(other.a));
        }

        public override bool Equals(object obj)
        {
            return obj is Edge other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return HashCode.Combine(a.x, a.y, a.z, b.x, b.y, b.z);
        }
    }
}
