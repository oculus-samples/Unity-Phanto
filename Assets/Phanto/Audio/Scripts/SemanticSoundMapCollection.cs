// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Phanto.Audio.Scripts
{
    public class SemanticSoundMapCollection : ScriptableObject
    {
        [Serializable]
        public class SemanticSoundMap
        {
            public string name;
            public PhantoRandomOneShotSfxBehavior oneShotPrefab;
        }

        [SerializeField]
        private SemanticSoundMap[] soundMaps;

        public IReadOnlyList<SemanticSoundMap> SoundMaps => soundMaps;

#if UNITY_EDITOR
        private void OnValidate()
        {
            var labels = new HashSet<string>();

            foreach (var soundMap in soundMaps)
            {
                if (!labels.Add(soundMap.name))
                {
                    Debug.LogWarning($"duplicate entry in soundsMaps?: {soundMap.name}");
                }
            }

            foreach (var classString in OVRSceneManager.Classification.List)
            {
                // Skip entries we don't care about.
                if (classString == "DESK" || classString == "GLOBAL_MESH")
                {
                    continue;
                }

                if (!labels.Contains(classString))
                {
                    Debug.LogWarning($"Unhandled entry in scene manager classification list: {classString}");
                }
            }
        }
#endif
    }
}
