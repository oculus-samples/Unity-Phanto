// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;

/// <summary>
/// This script manages the Tutorial game object visibility based on app input focus status
/// </summary>
[MetaCodeSample("Phanto")]
public class TutorialFocusManager : MonoBehaviour
{
    [SerializeField] private GameObject tutorialMainObject;
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
        tutorialMainObject.SetActive(false);
    }

    private void OnFocusAcquired()
    {
        tutorialMainObject.SetActive(true);
    }
}
