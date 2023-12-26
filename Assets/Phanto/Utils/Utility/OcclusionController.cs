// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Meta.XR.Depth;
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
        private OcclusionType occlusionType = OcclusionType.HardOcclusion;

        private readonly List<Renderer> renderers = new List<Renderer>();

        private void OnEnable()
        {
            GetComponentsInChildren<Renderer>(true, renderers);
            UpdateMaterialKeywords();
        }

        private void UpdateMaterialKeywords()
        {
            if (!OcclusionKeywordToggle.SupportsOcclusion)
            {
                Debug.LogWarning($"{nameof(OcclusionController)} unsupported device. Not setting occlusion keywords.");
                return;
            }

            foreach (var renderer in renderers)
            {
                var material = renderer.material;

                switch (occlusionType)
                {
                    case OcclusionType.HardOcclusion:
                        material.DisableKeyword(EnvironmentDepthOcclusionController.SoftOcclusionKeyword);
                        material.EnableKeyword(EnvironmentDepthOcclusionController.HardOcclusionKeyword);
                        break;
                    case OcclusionType.SoftOcclusion:
                        material.DisableKeyword(EnvironmentDepthOcclusionController.HardOcclusionKeyword);
                        material.EnableKeyword(EnvironmentDepthOcclusionController.SoftOcclusionKeyword);
                        break;
                    default:
                        material.DisableKeyword(EnvironmentDepthOcclusionController.HardOcclusionKeyword);
                        material.DisableKeyword(EnvironmentDepthOcclusionController.SoftOcclusionKeyword);
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
