// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Phantom;
using UnityEngine;

/// <summary>
/// This script manages the Polterblast game object visibility based on app input focus status
/// </summary>
public class PolterblastManager : MonoBehaviour
{
    [SerializeField] private GameObject Polterblast;
    private bool _isVisible;

    private void Awake()
    {
        OVRManager.InputFocusAcquired += OnFocusAcquired;
        OVRManager.InputFocusLost += OnFocusLost;
    }

    private void Start()
    {
        if (PhantomManager.Instance is TutorialPhantomManager)
        {
            _isVisible = true; // Show Polterblast when not in the game context
            OnFocusAcquired();
        }
        else if (GameplaySettingsManager.Instance.WavesAvailable)
        {
            // Polterblast visibility is dependant on the game current wave
            GameplaySettingsManager.Instance.OnNewWave.AddListener(OnNewWave);
        }
    }

    private void OnNewWave(GameplaySettings.WaveSettings newWaveSettings)
    {
        _isVisible = newWaveSettings.phantoSetting.isEnabled;
        Polterblast.SetActive(_isVisible);
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
        Polterblast.SetActive(_isVisible);
    }
}
