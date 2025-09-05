// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;

namespace PhantoUtils
{
    [MetaCodeSample("Phanto")]
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
