// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;

/// <summary>
/// Manages time spent in gameplay, including highscore management
/// </summary>
public class UIGameplayTimeManager : MonoBehaviour
{
    private const string HIGHSCORE_KEY = "PHANTO_HIGH_SCORE_";
    private const int MAX_HIGHSCORE = 3;
    private float[] bestTimes = { 300, 600, 900, 1100 };
    private float endTime;
    private bool registered;

    private float startTime;

    private void Awake()
    {
        startTime = Time.unscaledTime;

        for (var i = 0; i < MAX_HIGHSCORE; i++)
        {
            bestTimes[i] = PlayerPrefs.GetFloat(HIGHSCORE_KEY + i, bestTimes[i]);
        }

        Array.Sort(bestTimes);
    }

    /// <summary>
    /// Called when game ends
    /// </summary>
    public void OnEndGame()
    {
        if (!registered)
        {
            registered = true;
            endTime = Time.unscaledTime;
            RegisterPlayerTime();
        }
    }

    /// <summary>
    /// Called to get the time spent in gameplay
    /// </summary>
    public string GetPlayedTime()
    {
        var msg = $"TIME <color=#ffff00>{Time2String(endTime - startTime)}</color>";
        return msg;
    }

    /// <summary>
    /// Called to get the best times spent in gameplay
    /// </summary>
    private void RegisterPlayerTime()
    {
        bestTimes[MAX_HIGHSCORE] = endTime - startTime;

        Array.Sort(bestTimes);

        for (int i = 0; i < MAX_HIGHSCORE; i++)
        {
            PlayerPrefs.SetFloat(HIGHSCORE_KEY + i, bestTimes[i]);
        }
    }

    /// <summary>
    /// Called to get the best times spent in gameplay in ranking
    /// </summary>
    public string GetRanking()
    {
        var msg = "";
        var playerTime = endTime - startTime;
        string[] prefix = { "1st", "2nd", "3rd" };
        var index = 0;

        Array.Sort(bestTimes);

        foreach (var bTime in bestTimes)
        {
            if (index < MAX_HIGHSCORE)
            {
                var color = bTime == playerTime ? "#ffff00" : "#ffffff";
                msg += $"{prefix[index]} <color={color}>{Time2String(bTime)}</color>\n";
            }

            index++;
        }

        return msg;
    }

    private string Time2String(float time)
    {
        var ts = TimeSpan.FromSeconds(time);
        return string.Format("{0:00}:{1:00}", ts.Minutes, ts.Seconds);
    }
}
