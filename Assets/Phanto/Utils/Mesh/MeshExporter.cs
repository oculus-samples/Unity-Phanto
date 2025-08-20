// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;

public class MeshExporter : MonoBehaviour
{
    [SerializeField] private OVRInput.RawButton _exportButton;

    private void Update()
    {
        if (OVRInput.GetDown(_exportButton))
        {
            var meshFilter = GetComponent<MeshFilter>();
            MeshExporterObj.ExportObjToFile(meshFilter,
                $"{Application.persistentDataPath}/ExportedMesh_{gameObject.name}_{DateTime.Now.ToString("yy-MM-dd_HH-mm-ss")}.obj");
            enabled = false; // Only export once
        }
    }
}
