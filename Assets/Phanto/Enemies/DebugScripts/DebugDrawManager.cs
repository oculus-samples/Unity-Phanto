// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;

namespace Phanto.Enemies.DebugScripts
{
    public class DebugDrawManager : MonoBehaviour
    {
        public static bool DebugDraw { get; set; }

#if XR_GIZMOS

        private void Update()
        {
            var startIsDown = OVRInput.Get(OVRInput.Button.Start,
                OVRInput.Controller.LTouch | OVRInput.Controller.RTouch);

            // Hold down start and click the left thumbstick to enable debug drawing.
            if (startIsDown && OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch))
                DebugDraw = !DebugDraw;

#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.Escape)) DebugDraw = !DebugDraw;
#endif
            if (DebugDraw) DebugDrawEvent?.Invoke();
        }
#endif
        public static event Action DebugDrawEvent;
    }
}
