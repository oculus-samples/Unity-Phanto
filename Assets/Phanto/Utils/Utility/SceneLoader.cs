// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhantoUtils
{
    [DefaultExecutionOrder(1)]
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private Object sceneToLoad;

        [SerializeField]
        [Tooltip(
            "This is set automatically when sceneToLoad is set. If the scene name changes or this name is incorrect, update the value of sceneToLoad.")]
        private string _sceneNameToLoad;

        [SerializeField] private LoadSceneMode loadMode = LoadSceneMode.Additive;

        [SerializeField] private bool loadOnStart = true;

        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            // This should reduce frame drops during scene loads.
            Application.backgroundLoadingPriority = ThreadPriority.Low;
        }

        private void Start()
        {
            if (loadOnStart) LoadScene();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (sceneToLoad != null) _sceneNameToLoad = sceneToLoad.name;
        }
#endif

        public void LoadScene()
        {
            LoadSceneAsync();
        }

        public void UnloadScene()
        {
            UnloadSceneAsync();
        }

        public AsyncOperation LoadSceneAsync()
        {
            return SceneManager.LoadSceneAsync(_sceneNameToLoad, loadMode);
        }

        public AsyncOperation UnloadSceneAsync()
        {
            return SceneManager.UnloadSceneAsync(_sceneNameToLoad);
        }
    }
}
