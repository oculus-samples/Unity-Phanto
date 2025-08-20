// Copyright (c) Meta Platforms, Inc. and affiliates.

using OVRSimpleJSON;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildInfo : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI info;
    [SerializeField] private Text infoText;
    public static string VersionNumber { get; private set; }
    public static string BundleVersion { get; private set; }
    public static string Date { get; private set; }

    private void Start()
    {
        var output = $"v{VersionNumber}.{BundleVersion} {Date}";

        if (info != null) info.text = output;

        if (infoText != null) infoText.text = output;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void LoadInfo()
    {
        var textAsset = Resources.Load<TextAsset>("buildinfo");

        if (textAsset == null || string.IsNullOrEmpty(textAsset.text))
        {
            VersionNumber = "?";
            BundleVersion = "?";
            Date = "?";
            return;
        }

        var json = JSON.Parse(textAsset.text);

        VersionNumber = json["versionNumber"];
        BundleVersion = json["bundleVersion"];
        Date = json["date"];
    }
}
