// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Runtime.CompilerServices;
using Phanto.Enemies.DebugScripts;
using PhantoUtils;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Utilities.XR;

/// <summary>
///     Set start and end points for the navmeshlink
/// </summary>
public class NavMeshLinkController : MonoBehaviour
{
    [SerializeField] private NavMeshLink navMeshLink;
    private Vector3 _endPoint;

    private Vector3 _startPoint;

    private Transform _transform;

    private void Awake()
    {
        _transform = transform;
    }

    private void OnEnable()
    {
        DebugDrawManager.DebugDrawEvent += DebugDraw;
    }

    private void OnDisable()
    {
        DebugDrawManager.DebugDrawEvent -= DebugDraw;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (navMeshLink == null) navMeshLink = GetComponent<NavMeshLink>();
    }
#endif

    public void Initialize(Vector3 start, Vector3 end, int areaId = -1)
    {
        start = SnapPointToNavMesh(start);
        end = SnapPointToNavMesh(end);

        var forward = Vector3.ProjectOnPlane(end - start, Vector3.up).normalized;

        _transform.SetPositionAndRotation(start, Quaternion.LookRotation(forward, Vector3.up));

        navMeshLink.startPoint = _transform.InverseTransformPoint(start);
        navMeshLink.endPoint = _transform.InverseTransformPoint(end);

        if (areaId != -1) navMeshLink.area = areaId;

        _startPoint = start;
        _endPoint = end;
    }

    public void Destruct()
    {
        Destroy(gameObject);
    }

    private void DebugDraw()
    {
        XRGizmos.DrawPoint(_transform.position, MSPalette.Purple);
        XRGizmos.DrawPoint(_endPoint, MSPalette.Purple);
        XRGizmos.DrawLine(_startPoint, _endPoint, MSPalette.Purple, 0.008f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 SnapPointToNavMesh(Vector3 point, float maxDistance = 10.0f)
    {
        if (!NavMesh.SamplePosition(point, out var meshHit, maxDistance, NavMesh.AllAreas))
        {
            Debug.LogWarning($"No navmesh with near point!");
            return point;
        }

        return meshHit.position;
    }
}
