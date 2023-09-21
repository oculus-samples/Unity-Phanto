// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

/// <summary>
/// This script manages the Polterblast game object visibility based on app input focus status
/// </summary>
public class PolterblastManager : MonoBehaviour
{
    [SerializeField] private GameObject Polterblast;

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
        Polterblast.SetActive(false);
    }

    private void OnFocusAcquired()
    {
        Polterblast.SetActive(true);
    }
}
