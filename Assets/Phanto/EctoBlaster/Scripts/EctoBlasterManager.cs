// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

/// <summary>
/// This script manages the Ecto Blaster game object visibility based on app input focus status
/// </summary>
public class EctoBlasterManager : MonoBehaviour
{
    [SerializeField] private GameObject[] EctoBlaster;

    private void Awake()
    {
        OVRManager.InputFocusAcquired += OnFocusAcquired;
        OVRManager.InputFocusLost += OnFocusLost;
    }

    private void OnDestroy()
    {
        OVRManager.InputFocusAcquired -= OnFocusAcquired;
        OVRManager.InputFocusLost -= OnFocusLost;
    }

    private void OnFocusLost()
    {
        SetVisibility(false);
    }

    private void OnFocusAcquired()
    {
        SetVisibility(true);
    }

    private void SetVisibility(bool isVisible)
    {
        foreach (var element in EctoBlaster) element.SetActive(isVisible);
    }
}
