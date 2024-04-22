// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using PhantoUtils;
using UnityEngine;

namespace Phantom.Environment.Scripts
{
    /// <summary>
    ///     Represents a scene anchor, holdinf relevant information.
    /// </summary>
    public class PhantoAnchorInfo : MonoBehaviour
    {
        private OVRSceneAnchor _sceneAnchor;
        private OVRSemanticClassification semanticClassification;

        private void Awake()
        {
            _sceneAnchor = GetComponent<OVRSceneAnchor>();
        }

        private IEnumerator Start()
        {
            var isVolume = false;
            while (enabled)
            {
                if (TryGetComponent(out OVRSceneVolume _) || TryGetComponent(out OVRSceneVolumeMeshFilter _))
                {
                    isVolume = true;
                    break;
                }

                if (TryGetComponent(out OVRScenePlane _))
                {
                    isVolume = false;
                    break;
                }

                yield return null;
            }

            while (!_sceneAnchor.IsTracked) yield return null;

            while (!TryGetComponent(out semanticClassification)) yield return null;

            var handle = (ushort)_sceneAnchor.Space.Handle;

            gameObject.SetSuffix($"{semanticClassification.Labels[0]}_{handle:X4}_{(isVolume ? "V" : "P")}");
        }
    }
}
