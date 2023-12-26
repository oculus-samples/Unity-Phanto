// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
using UnityEngine.Rendering;
using Meta.XR.Depth;

namespace PhantoUtils
{
    public class OcclusionKeywordToggle : MonoBehaviour
    {
        private const string HARD_OCCLUSION = "HARD_OCCLUSION";
        private const string SOFT_OCCLUSION = "SOFT_OCCLUSION";

        private static GlobalKeyword _hardOcclusionKeyword;
        private static GlobalKeyword _softOcclusionKeyword;

        private OcclusionType _occlusionType = OcclusionType.NoOcclusion;
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
                _occlusionType = OcclusionType.SoftOcclusion;
            }

            if (Shader.IsKeywordEnabled(_hardOcclusionKeyword))
            {
                _occlusionType = OcclusionType.HardOcclusion;
            }

            // For HMDs that don't support occlusion turn off both keywords.
            if (!SupportsOcclusion)
            {
                _occlusionType = OcclusionType.NoOcclusion;
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
                if (++increment > (int)OcclusionType.SoftOcclusion)
                {
                    increment = 0;
                }

                _occlusionType = (OcclusionType)increment;

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

        private static void SetOcclusionState(OcclusionType occlusionType)
        {
            switch (occlusionType)
            {
                case OcclusionType.HardOcclusion:
                    Shader.SetKeyword(_hardOcclusionKeyword, true);
                    Shader.SetKeyword(_softOcclusionKeyword, false);
                    break;
                case OcclusionType.SoftOcclusion:
                    Shader.SetKeyword(_hardOcclusionKeyword, false);
                    Shader.SetKeyword(_softOcclusionKeyword, true);
                    break;
                default:
                    Shader.SetKeyword(_hardOcclusionKeyword, false);
                    Shader.SetKeyword(_softOcclusionKeyword, false);
                    break;
            }
        }

        public static bool SupportsOcclusion
        {
            get
            {
                switch (OVRPlugin.GetSystemHeadsetType())
                {
                    case OVRPlugin.SystemHeadset.Meta_Quest_3:
                    // case OVRPlugin.SystemHeadset.Meta_Link_Quest_3: // TODO: Coming soon.
                        return true;
                }

                return false;
            }
        }
    }
}
