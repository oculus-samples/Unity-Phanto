// Copyright (c) Meta Platforms, Inc. and affiliates.

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Common.Editor
{
    public static class ApplicationPersistentDataPathMenuItem
    {
        [MenuItem("File/Open Application Persistent Data Path")]
        private static void OpenApplicationPersistentDataPath()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
    }
}

#endif
