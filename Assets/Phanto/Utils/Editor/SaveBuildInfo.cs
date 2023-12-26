// Copyright (c) Meta Platforms, Inc. and affiliates.

#if UNITY_EDITOR

using System;
using System.IO;
using OVRSimpleJSON;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class SaveBuildInfo : IPreprocessBuildWithReport
{
    public int callbackOrder => -10;

    public void OnPreprocessBuild(BuildReport report)
    {
        GenerateBuildInfo();
    }

    private static void GenerateBuildInfo()
    {
        var output = new JSONObject();

        // get version number
        output["versionNumber"] = PlayerSettings.bundleVersion;

#if UNITY_ANDROID
        // get android build number
        output["bundleVersion"] = PlayerSettings.Android.bundleVersionCode.ToString();
#else
        output["bundleVersion"] = "N/A";
#endif
        // get UTC time stamp
        output["date"] = DateTime.UtcNow.ToString("u");

        // Write all of them to JSON file
        var path = Path.Combine(Application.dataPath, "Resources");

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        path = Path.Combine(path, "buildinfo.json");

        File.WriteAllText(path, output.ToString(2));

        AssetDatabase.Refresh();
    }
}

#endif
