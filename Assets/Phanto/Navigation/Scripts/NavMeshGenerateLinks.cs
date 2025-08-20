// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Generates UnityEngine.AI.NavMeshLink objects from a list of Nav
/// </summary>
public static class NavMeshGenerateLinks
{
    public static void GenInternalLinks(List<NavMeshTriangle> navMeshTriangles,
        NavMeshLinkController navMeshLinkPrefab, Transform transform)
    {
        if (navMeshTriangles == null)
        {
            Debug.LogWarning($"[{nameof(GenInternalLinks)}] No navmesh triangles on {transform.parent.name}");
            return;
        }

        var corners = new Vector3[1024];

        NavMeshTriangle prime = null;
        var maxArea = float.MinValue;

        // Find the largest open border triangle in the navmesh.
        foreach (var triangle in navMeshTriangles)
        {
            if (!triangle.IsOpen || !triangle.IsBorder) continue;
            if (triangle.Area > maxArea)
            {
                maxArea = triangle.Area;
                prime = triangle;
            }
        }

        if (prime == null)
        {
            Debug.Log("No open triangles");
            return;
        }

        var areaMask = 1 << prime.areaId;

        foreach (var other in navMeshTriangles)
        {
            // We're not going to link triangles that aren't "open".
            if (prime == other || !other.IsOpen || !other.IsBorder) continue;

            var path = new NavMeshPath();

            // Is there a path from prime triangle to other?
            var success = NavMesh.CalculatePath(prime.center, other.center, areaMask, path);

            if (path.status == NavMeshPathStatus.PathComplete) continue;

            if (!success || path.status == NavMeshPathStatus.PathInvalid)
            {
                // This shouldn't be possible since both start and end points are definitely on navmesh.
                Debug.LogError($"Unreachable nodes! a:{prime} b:{other}");
                continue;
            }

            // Path is partially complete.
            // Get the corners of the path. the last corner should be as close as we can get to the other border.
            var cornerCount = path.GetCornersNonAlloc(corners);

            var midpoint = corners[cornerCount - 1];
            if (cornerCount > 1) midpoint = Vector3.Lerp(corners[cornerCount - 2], midpoint, 0.9f);

            // Path going the other way
            success = NavMesh.CalculatePath(other.center, midpoint, areaMask, path);
            Vector3 endPoint;
            if (!success || path.status == NavMeshPathStatus.PathInvalid)
            {
                NavMesh.Raycast(other.center, midpoint, out var hit, areaMask);

                endPoint = Vector3.Lerp(other.center, hit.position, 0.9f);
            }
            else
            {
                cornerCount = path.GetCornersNonAlloc(corners);

                endPoint = corners[cornerCount - 1];
                if (cornerCount > 1) endPoint = Vector3.Lerp(corners[cornerCount - 2], endPoint, 0.9f);
            }

            var link = Object.Instantiate(navMeshLinkPrefab, transform);
            link.Initialize(midpoint, endPoint, prime.areaId);
        }
    }
}
