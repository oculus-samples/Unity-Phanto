// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Meta.XR.EnvironmentDepth;
using UnityEditor;
using UnityEngine;

namespace PhantoUtils
{
    /// <summary>
    /// Sets occlusion type for this object.
    /// </summary>
    public class OcclusionController : MonoBehaviour
    {
        [SerializeField]
        private OcclusionShadersMode occlusionType = OcclusionShadersMode.HardOcclusion;

        private readonly List<Renderer> renderers = new List<Renderer>();

        private void OnEnable()
        {
            GetComponentsInChildren<Renderer>(true, renderers);
            UpdateMaterialKeywords();
        }

        private void UpdateMaterialKeywords()
        {
            if (!EnvironmentDepthManager.IsSupported)
            {
                Debug.LogWarning($"{nameof(OcclusionController)} unsupported device. Not setting occlusion keywords.");
                return;
            }

            foreach (var renderer in renderers)
            {
                var material = renderer.material;

                switch (occlusionType)
                {
                    case OcclusionShadersMode.HardOcclusion:
                        material.DisableKeyword(EnvironmentDepthManager.SoftOcclusionKeyword);
                        material.EnableKeyword(EnvironmentDepthManager.HardOcclusionKeyword);
                        break;
                    case OcclusionShadersMode.SoftOcclusion:
                        material.DisableKeyword(EnvironmentDepthManager.HardOcclusionKeyword);
                        material.EnableKeyword(EnvironmentDepthManager.SoftOcclusionKeyword);
                        break;
                    default:
                        material.DisableKeyword(EnvironmentDepthManager.HardOcclusionKeyword);
                        material.DisableKeyword(EnvironmentDepthManager.SoftOcclusionKeyword);
                        break;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EditorApplication.isPlaying)
            {
                UpdateMaterialKeywords();
            }
        }
#endif
    }
}
