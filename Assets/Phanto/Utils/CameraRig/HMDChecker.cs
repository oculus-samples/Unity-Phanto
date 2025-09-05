// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.Events;

[MetaCodeSample("Phanto")]
public class HMDChecker : MonoBehaviour
{
    [SerializeField] private UnityEvent HMDDetectedEvent;

    public void ExecuteIfHMDDetected()
    {

#if UNITY_EDITOR
        if (!OVRManager.isHmdPresent)
        {
            Debug.LogWarning("No HMD detected. Some features might not be available.", this);
            return;
        }
#endif

        HMDDetectedEvent?.Invoke();
    }
}
