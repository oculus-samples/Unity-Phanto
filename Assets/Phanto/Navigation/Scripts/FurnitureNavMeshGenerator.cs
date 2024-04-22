// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Phanto.Enemies.DebugScripts;
using PhantoUtils;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using static NavMeshConstants;
using static NavMeshGenerateLinks;

/// <summary>
///     This script generates the navigation mesh for a given furniture.
/// </summary>
public class FurnitureNavMeshGenerator : MonoBehaviour
{
    private static readonly List<FurnitureNavMeshGenerator> furnitureNavMesh = new();
    public static IReadOnlyList<FurnitureNavMeshGenerator> FurnitureNavMesh => furnitureNavMesh;

    [SerializeField] private NavMeshSurface navMeshSurface;

    [SerializeField] private NavMeshLinkController navMeshLinkPrefab;

    private readonly List<NavMeshLinkController> _navMeshLinks = new();

    private readonly List<NavMeshTriangle> _validTriangles = new();

    private int _areaMask;
    private List<List<Vector3>> _loops;
    private List<NavMeshTriangle> _navMeshTriangles;
    private readonly List<NavMeshTriangle> _shuffledTriangles = new();

    private Bounds _objectBounds;

    private Transform _transform;

    public bool HasNavMesh => _validTriangles.Count > 0;

    public OVRSemanticClassification Classification { get; private set; }

    private void Awake()
    {
        _transform = transform;
        NavMeshBookKeeper.OnValidateScene += GenerateInternalLinks;
    }

    private void OnEnable()
    {
        furnitureNavMesh.Add(this);

        DebugDrawManager.DebugDrawEvent += OnDebugDraw;
    }

    private void OnDisable()
    {
        furnitureNavMesh.Remove(this);

        DebugDrawManager.DebugDrawEvent -= OnDebugDraw;
    }

    private void OnDestroy()
    {
        NavMeshBookKeeper.OnValidateScene -= GenerateInternalLinks;

        foreach (var link in _navMeshLinks) link.Destruct();

        navMeshSurface.ClearNavMeshTriangles();
    }

    public bool Initialize(OVRSemanticClassification classification)
    {
        Classification = classification;

        var anchorTransform = classification.transform;

        var anchorPosition = anchorTransform.position;

        navMeshSurface.defaultArea = FurnitureArea;
        _areaMask = FurnitureAreaMask;

#if USE_OBJECT_MESH
        var meshFilter = classification.GetComponentInChildren<MeshFilter>();

        // Set position and size for NavMeshSurface to cover the furniture.
        var mesh = meshFilter.mesh;
        var verts = mesh.vertices;
        var tris = mesh.triangles;

        var meshTransform = meshFilter.transform;

        var bounds = new Bounds(meshTransform.TransformPoint(verts[0]), Vector3.zero);
        var triCount = tris.Length;

        for (var i = 0; i < triCount; i += 3)
        {
            var (i1, i2, i3) = (tris[i], tris[i + 1], tris[i + 2]);

            var v1 = meshTransform.TransformPoint(verts[i1]);
            var v2 = meshTransform.TransformPoint(verts[i2]);
            var v3 = meshTransform.TransformPoint(verts[i3]);

            bounds.Encapsulate(v1);
            bounds.Encapsulate(v2);
            bounds.Encapsulate(v3);
        }

        _objectBounds = bounds;
#else
        // Use the dimensions of the volume component to size the navmesh volume.
        var bounds = new Bounds(transform.position, Vector3.zero);

        if (classification.TryGetComponent(out OVRSceneVolume volume))
        {
            var size = volume.Dimensions;
            (size.y, size.z) = (size.z, size.y);

            var offset = volume.Offset;
            (offset.y, offset.z) = (offset.z, offset.y);

            anchorPosition += offset;
            bounds.center += offset;

            // Furniture navmesh volume doesn't go all the way to the floor
            // to avoid overlap with floor navmesh.
            size.y *= 0.75f;

            bounds.size = size;
            var center = bounds.center;
            center.y -= bounds.extents.y;
            bounds.center = center;
        }
        else
        {
            Debug.LogWarning($"No volume component on furniture '{name}'. Navmesh may not be valid.", this);
        }

        _objectBounds = bounds;
#endif

        // Furniture spatial anchors have "forward" as the up axis and "-up" is the forward.
        transform.SetPositionAndRotation(anchorPosition,
            Quaternion.LookRotation(-anchorTransform.up, Vector3.up));

        return BuildNavMesh(bounds);
    }

    private bool BuildNavMesh(Bounds bounds)
    {
        navMeshSurface.center = _transform.InverseTransformPoint(bounds.center);
        navMeshSurface.size = new Vector3(bounds.size.x, Mathf.Max(bounds.size.y, 0.002f), bounds.size.z);
        _navMeshTriangles = navMeshSurface.GenerateNavMeshTriangles();

        var success = _navMeshTriangles != null;
        if (success) GenerateLinks(bounds);

        return success;
    }

    private void ValidateTriangles()
    {
        _validTriangles.Clear();

        if (_navMeshTriangles == null)
        {
            Debug.LogWarning($"[{nameof(ValidateTriangles)}] No navmesh triangles on {transform.parent.name}");
            return;
        }

        foreach (var tri in _navMeshTriangles)
            if (tri.IsOpen)
                _validTriangles.Add(tri);

        if (_validTriangles.Count == 0)
        {
            // This can happen if, for example, a ceiling fan or chandelier is directly over a table.
            var parent = transform.parent;
            Debug.LogWarning($"furniture is entirely covered (under other object?). [{parent.name}]", parent);
            _validTriangles.AddRange(_navMeshTriangles);
        }
    }

    /// <summary>
    ///     Used by the custom inspector button
    /// </summary>
    internal void BuildNavMesh()
    {
        BuildNavMesh(_objectBounds);
    }

    /// <summary>
    ///     Used by the custom inspector button
    /// </summary>
    internal void RemoveNavMesh()
    {
        navMeshSurface.RemoveData();
    }

    // Create a link from the navmesh on the furniture to the floor.
    private bool GenerateLinks(Bounds bounds)
    {
        // Get rid of previous links.
        foreach (var prevLink in _navMeshLinks) prevLink.Destruct();
        _navMeshLinks.Clear();

        var pos = _transform.position;

        if (NavMesh.SamplePosition(pos, out var navMeshHit, 10.0f, _areaMask)) pos = navMeshHit.position;

        // FIXME: handle users who've marked furniture backwards (forward == -forward?
        var forward = _transform.forward;
        var ray = new Ray(pos, forward);

        // Cast a navmesh ray forward from the top center of the volume to try and find the front edge of the object's surface.
        var topExtent = Mathf.Max(bounds.extents.x, bounds.extents.z);
        NavMesh.Raycast(pos, ray.GetPoint(topExtent), out navMeshHit, _areaMask);

        // Back away from the edge a small amount. This with be the hop destination.
        var edgePoint = navMeshHit.position;
        var endPoint = ray.GetPoint(navMeshHit.distance * 0.9f);

        if (NavMesh.SamplePosition(endPoint, out navMeshHit, 10.0f, _areaMask)) endPoint = navMeshHit.position;

        ray = new Ray(edgePoint, forward);

        var dropPoint = ray.GetPoint(0.5f);

        var floorPoint = dropPoint;

        // Sphere cast downwards over the edge to find a point on the floor. This will be the hop starting point.
        if (Physics.SphereCast(dropPoint, TennisBall, Vector3.down, out var raycastHit, 10.0f, SceneMeshLayerMask,
                QueryTriggerInteraction.Ignore)) floorPoint = raycastHit.point;

        if (!NavMesh.SamplePosition(floorPoint, out navMeshHit, 10.0f, FloorAreaMask))
        {
            // This is an indication that the scene mesh failed to load.
            Debug.LogError($"There's no floor? [{transform.parent.name}]", this);
            return false;
        }

        floorPoint = navMeshHit.position;

        // instantiate link from edge to floor point.
        var link = Instantiate(navMeshLinkPrefab, transform);
        link.Initialize(endPoint, floorPoint);

        _navMeshLinks.Add(link);
        return true;
    }

    private void GenerateInternalLinks()
    {
        ValidateTriangles();
        GenInternalLinks(_navMeshTriangles, navMeshLinkPrefab, transform);
    }

    public Vector3 RandomPoint(float padding = 0.025f)
    {
        if (_validTriangles.Count == 0) ValidateTriangles();

        _shuffledTriangles.Clear();
        _shuffledTriangles.AddRange(_validTriangles);

        _shuffledTriangles.Shuffle();
        NavMeshBookKeeper.FindMatchingPoint(_shuffledTriangles, out var result, PaddingFromEdge);

        return result;

        bool PaddingFromEdge(Vector3 point, NavMeshTriangle tri)
        {
            // get the distance from point to edge of the surface.
            return NavMesh.FindClosestEdge(point, out var navMeshHit, tri.AreaMask) && navMeshHit.distance <= padding;
        }
    }

    private NavMeshTriangle RandomTriangle()
    {
        if (_validTriangles.Count == 0) ValidateTriangles();

        return _validTriangles.RandomElement();
    }

    public void Destruct()
    {
        Destroy(gameObject);
    }

    private void OnDebugDraw()
    {
        foreach (var triangle in _navMeshTriangles) triangle.DebugDraw(MSPalette.Chartreuse, MSPalette.Aqua);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        if (_loops == null) return;

        foreach (var loop in _loops)
        {
            for (var i = 1; i < loop.Count; i++)
            {
                Gizmos.DrawLine(loop[i - 1], loop[i]);
            }

            Gizmos.DrawLine(loop[loop.Count - 1], loop[0]);
        }
    }

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
[CustomEditor(typeof(FurnitureNavMeshGenerator))]
public class FurnitureNavMeshGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (EditorApplication.isPlaying)
        {
            var generator = target as FurnitureNavMeshGenerator;
            GUILayout.Space(16);
            if (GUILayout.Button("Bake")) generator.BuildNavMesh();
            GUILayout.Space(8);
            if (GUILayout.Button("Remove")) generator.RemoveNavMesh();
        }
    }
}
#endif
