// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class MeshExporterObj
{
    public static (string, int) ConvertToObjString(MeshFilter meshFilter, int triangleStartIndex = 0)
    {
        var numVertices = 0;
        var mesh = meshFilter.sharedMesh;
        var materials = meshFilter.GetComponent<Renderer>().sharedMaterials;

        var sb = new StringBuilder();

        foreach (var v in mesh.vertices)
        {
            numVertices++;
            sb.Append($"v {-v.x} {v.y} {v.z}\n");
        }

        sb.Append("\n");
        foreach (var n in mesh.normals) sb.Append($"vn {-n.x} {n.y} {n.z}\n");

        sb.Append("\n");
        foreach (Vector3 uv in mesh.uv) sb.Append($"vt {uv.x} {uv.y}\n");

        for (var material = 0; material < mesh.subMeshCount; material++)
        {
            sb.Append("\n");
            sb.Append("usemtl ").Append(materials[material].name).Append("\n");
            sb.Append("usemap ").Append(materials[material].name).Append("\n");

            var triangles = mesh.GetTriangles(material);
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var idx2 = triangles[i + 0] + 1 + triangleStartIndex;
                var idx1 = triangles[i + 1] + 1 + triangleStartIndex;
                var idx0 = triangles[i + 2] + 1 + triangleStartIndex;
                sb.Append($"f {idx0}/{idx0}/{idx0} {idx1}/{idx1}/{idx1} {idx2}/{idx2}/{idx2}\n");
            }
        }

        return (sb.ToString(), numVertices);
    }

    public static void ExportObjToFile(MeshFilter meshFilter, string filePath)
    {
        var (objString, numVertices) = ConvertToObjString(meshFilter);

        Debug.Log($"Exporting mesh with {numVertices} vertices to {filePath}.");
        CreateDirectoryIfNotExists(Application.persistentDataPath);
        _ = File.WriteAllTextAsync(filePath, objString);
    }

    private static void CreateDirectoryIfNotExists(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not create directory: {directory}");
            Debug.LogException(e);
        }
    }
}
