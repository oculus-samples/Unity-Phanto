// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEngine;
using UnityEngine.Rendering;
using Meta.XR.EnvironmentDepth;

namespace PhantoUtils
{
    public class OcclusionKeywordToggle : MonoBehaviour
    {
        private const string HARD_OCCLUSION = "HARD_OCCLUSION";
        private const string SOFT_OCCLUSION = "SOFT_OCCLUSION";

        private static GlobalKeyword _hardOcclusionKeyword;
        private static GlobalKeyword _softOcclusionKeyword;

        private OcclusionShadersMode _occlusionType = OcclusionShadersMode.None;
        private bool _released = true;

        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            _hardOcclusionKeyword = GlobalKeyword.Create(HARD_OCCLUSION);
            _softOcclusionKeyword = GlobalKeyword.Create(SOFT_OCCLUSION);
        }

        private void Start()
        {
            if (Shader.IsKeywordEnabled(_softOcclusionKeyword))
            {
                _occlusionType = OcclusionShadersMode.SoftOcclusion;
            }

            if (Shader.IsKeywordEnabled(_hardOcclusionKeyword))
            {
                _occlusionType = OcclusionShadersMode.HardOcclusion;
            }

            // For HMDs that don't support occlusion turn off both keywords.
            if (!EnvironmentDepthManager.IsSupported)
            {
                _occlusionType = OcclusionShadersMode.None;
                SetOcclusionState(_occlusionType);
            }
        }

        private void Update()
        {
            var startIsDown = OVRInput.Get(OVRInput.Button.Start, OVRInput.Controller.All);
            var xIsDown = OVRInput.Get(OVRInput.RawButton.X, OVRInput.Controller.LTouch);
            var yIsDown = OVRInput.Get(OVRInput.RawButton.Y, OVRInput.Controller.LTouch);

            if (startIsDown && xIsDown && yIsDown && _released)
            {
                var increment = (int)_occlusionType;
                if (++increment > (int)OcclusionShadersMode.SoftOcclusion)
                {
                    increment = 0;
                }

                _occlusionType = (OcclusionShadersMode)increment;

                SetOcclusionState(_occlusionType);

                Debug.Log($"Occlusion state: {_occlusionType}", this);
                _released = false;
            }

            if (OVRInput.GetUp(OVRInput.RawButton.X, OVRInput.Controller.LTouch) ||
                OVRInput.GetUp(OVRInput.RawButton.Y, OVRInput.Controller.LTouch))
            {
                _released = true;
            }
        }

        private static void SetOcclusionState(OcclusionShadersMode occlusionType)
        {
            switch (occlusionType)
            {
                case OcclusionShadersMode.HardOcclusion:
                    Shader.SetKeyword(_hardOcclusionKeyword, true);
                    Shader.SetKeyword(_softOcclusionKeyword, false);
                    break;
                case OcclusionShadersMode.SoftOcclusion:
                    Shader.SetKeyword(_hardOcclusionKeyword, false);
                    Shader.SetKeyword(_softOcclusionKeyword, true);
                    break;
                default:
                    Shader.SetKeyword(_hardOcclusionKeyword, false);
                    Shader.SetKeyword(_softOcclusionKeyword, false);
                    break;
            }
        }

    }
}
