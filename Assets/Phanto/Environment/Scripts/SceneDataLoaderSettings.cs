// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using Phantom.Environment.Scripts;
using UnityEngine;

namespace Phantom.Environment.Scripts
{
    public class SceneDataLoaderSettings : ScriptableObject
    {
        // JSON scene file.
        [SerializeField] private TextAsset sceneJson;

        // Whether to load scene data on Start.
        [SerializeField] private bool loadSceneOnStart = true;

        [SerializeField]
        private SceneDataLoader.SceneDataSource sceneDataSource = SceneDataLoader.SceneDataSource.SceneApi;

        [SerializeField] private bool centerStaticMesh = true;

        public string SceneJson => sceneJson != null ? sceneJson.text : null;
        public bool LoadSceneOnStart => loadSceneOnStart;
        public SceneDataLoader.SceneDataSource SceneDataSource => sceneDataSource;
        public bool CenterStaticMesh => centerStaticMesh;
    }
}
