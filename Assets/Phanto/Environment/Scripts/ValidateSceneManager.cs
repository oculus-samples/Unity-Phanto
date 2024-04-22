// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

public class ValidateSceneManager : MonoBehaviour
{
#if UNITY_EDITOR
    private void Awake()
    {
        if (!TryGetComponent<OVRSceneManager>(out var sceneManager))
        {
            Debug.LogError("This script must be attached to GameObject with OVRSceneManager");

        }

        Validate(sceneManager);
    }

    private void Validate(OVRSceneManager sceneManager)
    {
        if (sceneManager.PlanePrefab == null)
        {
            Debug.LogWarning("Plane prefab is null!", sceneManager);
        }

        if (sceneManager.VolumePrefab == null)
        {
            Debug.LogWarning("Volume prefab is null!", sceneManager);
        }

        var classifications = new HashSet<string>();

        foreach (var prefabOverride in sceneManager.PrefabOverrides)
        {
            var label = prefabOverride.ClassificationLabel;

            if (!classifications.Add(label))
            {
                Debug.LogWarning($"Duplicate label in the prefab overrides: {label}", sceneManager);
            }

            if (prefabOverride.Prefab == null)
            {
                Debug.LogWarning($"Null prefab for label: {label}!", sceneManager);
            }
        }
    }

    private void OnValidate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (TryGetComponent<OVRSceneManager>(out var sceneManager))
        {
            Validate(sceneManager);
        }
    }
#endif
}
