// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using UnityEngine;

public class SetFPS : MonoBehaviour
{
    [SerializeField] private int requestedFps = 72;

    private void OnEnable()
    {
        OVRManager.DisplayRefreshRateChanged += OnDisplayRefreshRateChanged;
    }

    private void OnDisable()
    {
        OVRManager.DisplayRefreshRateChanged -= OnDisplayRefreshRateChanged;
    }

    private void OnDisplayRefreshRateChanged(float previous, float current)
    {
        Debug.LogWarning($"OnDisplayRefreshRateChanged: prev: {previous} current: {current}");
    }

    private IEnumerator Start()
    {
        while (OVRManager.instance == null)
        {
            yield return null;
        }

        yield return null;

        OVRManager.display.displayFrequency = requestedFps;
    }
}
