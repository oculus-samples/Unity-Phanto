// Copyright (c) Meta Platforms, Inc. and affiliates.

#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// The ProjectSettings file gets modified during builds.
/// The changes are removed at the end of the build, but
/// that second modification doesn't get written to disk (until now).
/// </summary>
public class SaveProjectAfterBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => int.MaxValue; // execute last

    public void OnPostprocessBuild(BuildReport report)
    {
        // wait until the frame after build completes.
        EditorApplication.update += SaveAssets;
    }

    private static void SaveAssets()
    {
        EditorApplication.update -= SaveAssets;
        AssetDatabase.SaveAssets();
    }
}

#endif
