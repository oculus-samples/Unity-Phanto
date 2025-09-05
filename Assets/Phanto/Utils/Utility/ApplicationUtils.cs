// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;

namespace PhantoUtils.VR
{
    [MetaCodeSample("Phanto")]
    public static class ApplicationUtils
    {
        public static bool IsDesktop()
        {
            return Application.isEditor && !IsOculusLink();
        }

        public static bool IsOculusLink()
        {
            return Application.platform == RuntimePlatform.WindowsEditor && OVRManager.isHmdPresent;
        }

        public static bool IsVR()
        {
            return Application.platform == RuntimePlatform.Android;
        }
    }
}
