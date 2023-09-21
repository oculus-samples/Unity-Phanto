// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace PhantoUtils.VR
{
    public class EventSystemInitializer : MonoBehaviour
    {
        [SerializeField] private GameObject runtimeEventSystemPrefab;
        [SerializeField] private GameObject editorEventSystemPrefab;
        [SerializeField] private bool forceUseRuntimePrefab;

        private void Awake()
        {
            var isRuntime = forceUseRuntimePrefab || ApplicationUtils.IsOculusLink() || !ApplicationUtils.IsDesktop();
            var go = Instantiate(isRuntime ? runtimeEventSystemPrefab : editorEventSystemPrefab);
        }
    }
}
