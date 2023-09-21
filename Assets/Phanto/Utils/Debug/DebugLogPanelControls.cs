// Copyright (c) Meta Platforms, Inc. and affiliates.

#define LOAD_DEBUG_PANEL // Remove if you don't want the debug panel to be available.

using System;
using UnityEngine;

namespace Common
{
    public class DebugLogPanelControls : MonoBehaviour
    {
        public static Action<bool> DebugMenuEvent;

        [SerializeField] private GameObject[] menuGameObjects = Array.Empty<GameObject>();

        private bool _menuVisible;

        private void Start()
        {
            foreach (var go in menuGameObjects) go.SetActive(false);
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1) || OVRInput.GetDown(OVRInput.Button.Start))
            {
                _menuVisible = !_menuVisible;
                DebugMenuEvent?.Invoke(_menuVisible);

                foreach (var go in menuGameObjects) go.SetActive(_menuVisible);
            }
        }

#if LOAD_DEBUG_PANEL
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
#endif
        private static void Initialize()
        {
            var debugPrefab = Resources.Load<GameObject>("DebugPanel");

            if (debugPrefab == null)
            {
                Debug.LogError("Can't find debug panel prefab.");
                return;
            }

            Instantiate(debugPrefab);
        }
    }
}
