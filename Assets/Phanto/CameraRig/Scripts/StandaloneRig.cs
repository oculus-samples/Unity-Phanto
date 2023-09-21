// Copyright (c) Meta Platforms, Inc. and affiliates.

using PhantoUtils.VR;
using UnityEngine;

/// <summary>
/// Instantiates a CameraRig object if not present
/// </summary>
public class StandaloneRig : MonoBehaviour
{
    [SerializeField] private GameObject CameraRigPrefab;

    private void Awake()
    {
        if (FindObjectOfType<CameraRig>() == null) Instantiate(CameraRigPrefab);
        gameObject.SetActive(false);
    }
}
