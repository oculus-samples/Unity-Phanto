// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEngine;

namespace PhantoUtils
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1)]
    public class DontDestroyOnLoad : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(transform.root);
        }
    }
}
