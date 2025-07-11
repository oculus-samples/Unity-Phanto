// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Meta.XR.MRUtilityKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

public class ValidateSceneManager : MonoBehaviour
{
#if UNITY_EDITOR
    private void Awake()
    {
        if (!TryGetComponent<AnchorPrefabSpawner>(out var sceneManager))
        {
            Debug.LogError("This script must be attached to GameObject with SceneDataLoader");

        }

        Validate(sceneManager);
    }

    private void Validate(AnchorPrefabSpawner sceneManager)
    {

        if (sceneManager.PrefabsToSpawn.Select((group => group.Labels)).Where(labels => labels == MRUKAnchor.SceneLabels.CEILING).ToList().Count > 0)
        {
            Debug.LogWarning("Plane prefab is null!", sceneManager);
        }

        if (sceneManager.PrefabsToSpawn.Select((group => group.Labels)).Where(labels => labels == MRUKAnchor.SceneLabels.COUCH).ToList().Count > 0)
        {
            Debug.LogWarning("Volume prefab is null!", sceneManager);
        }

        var classifications = new HashSet<string>();

        foreach (var prefabOverride in sceneManager.PrefabsToSpawn.Select(group => group))
        {
            var label = prefabOverride;

            if (!classifications.Add(label.Labels.ToString()))
            {
                Debug.LogWarning($"Duplicate label in the prefab overrides: {label}", sceneManager);
            }

            if (prefabOverride == null)
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

        if (TryGetComponent<AnchorPrefabSpawner>(out var sceneManager))
        {
            Validate(sceneManager);
        }
    }
#endif
}
