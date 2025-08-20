// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
using UnityEngine.Events;

public class ButtonEventTrigger : MonoBehaviour
{
    [SerializeField] private OVRInput.RawButton button;
    [SerializeField] private KeyCode keycode;
    [SerializeField] private UnityEvent eventToRaise;

    private void Update()
    {
        if (OVRInput.GetDown(button) || Input.GetKeyDown(keycode)) eventToRaise?.Invoke();
    }
}
