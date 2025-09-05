// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using System.Collections;
using System.Collections.Generic;
using Phantom.Environment.Scripts;
using UnityEngine;

namespace Phantom.Environment.Scripts
{
    [MetaCodeSample("Phanto")]
    public class SceneDataLoaderSettings : ScriptableObject
    {

        // Whether to load scene data on Start.
        [SerializeField] private bool loadSceneOnStart = true;

        [SerializeField]
        private SceneDataLoader.SceneDataSource sceneDataSource = SceneDataLoader.SceneDataSource.SceneApi;

        [SerializeField] private bool centerStaticMesh = true;

        public bool LoadSceneOnStart => loadSceneOnStart;
        public SceneDataLoader.SceneDataSource SceneDataSource => sceneDataSource;
        public bool CenterStaticMesh => centerStaticMesh;
    }
}
