// Copyright (c) Meta Platforms, Inc. and affiliates.

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
