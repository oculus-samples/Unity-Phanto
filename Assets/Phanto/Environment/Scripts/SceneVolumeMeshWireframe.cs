// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates a volume mesh wireframe for the scene.
/// </summary>
[MetaCodeSample("Phanto")]
public class SceneVolumeMeshWireframe : MonoBehaviour
{
    [Tooltip("The mesh filter containing the mesh to be rendered.")]
    [SerializeField] private MeshFilter meshFilter;

    [Tooltip("The MeshFilter mesh filter.")]
    [SerializeField] private MeshFilter volumeMeshFilter;

    private MRUKAnchor _parentAnchor;

    private Mesh _mesh;

    private IEnumerator Start()
    {
        yield return null;

        var parentMeshFilter = volumeMeshFilter.GetComponent<MeshFilter>();
        _parentAnchor = parentMeshFilter.gameObject.GetComponentInParent<MRUKAnchor>();
        var parentMesh = _parentAnchor.GlobalMesh;
        parentMeshFilter.sharedMesh = _parentAnchor.GlobalMesh;

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        parentMesh.GetVertices(vertices);
        parentMesh.GetTriangles(triangles, 0);

        var c = new Color[triangles.Count];
        var v = new Vector3[triangles.Count];
        var idx = new int[triangles.Count];
        for (var i = 0; i < triangles.Count; i++)
        {
            c[i] = new Color(
                i % 3 == 0 ? 1.0f : 0.0f,
                i % 3 == 1 ? 1.0f : 0.0f,
                i % 3 == 2 ? 1.0f : 0.0f);
            v[i] = vertices[triangles[i]];
            idx[i] = i;
        }

        _mesh = new Mesh
        {
            indexFormat = IndexFormat.UInt32
        };
        _mesh.SetVertices(v);
        _mesh.SetColors(c);
        _mesh.SetIndices(idx, MeshTopology.Triangles, 0, true, 0);
        _mesh.RecalculateBounds();
        _mesh.RecalculateNormals();

        meshFilter.sharedMesh = _mesh;
    }

    private void OnDestroy()
    {
        Destroy(_mesh);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        OnValidate();
    }

    private void OnValidate()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

        if (volumeMeshFilter == null) volumeMeshFilter = GetComponentInParent<MeshFilter>();
    }
#endif
}
