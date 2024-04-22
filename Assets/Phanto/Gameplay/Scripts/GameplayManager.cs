// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;

public class GameplayManager : MonoBehaviour
{
    [Header("Logic points")]
    [SerializeField] private int maxGoos = 150;

    public UnityEvent OnGameWon;
    public UnityEvent OnGameLost;

    // Phantoms
    public UnityEvent<string> OnPhantomScoreChange;
    public UnityEvent<float> OnPhantomScorePercentageChange;

    // Goos
    public UnityEvent<string> OnGooScoreChange;
    public UnityEvent<float> OnGooPercentageScoreChange;

    // Phanto
    public UnityEvent<string> OnPhantoScoreChange;
    public UnityEvent<float> OnPhantoPercentageScoreChange;

    public int MaxGoos => maxGoos;

    public void OnGameOver(bool hasWon)
    {
        if (hasWon)
            OnGameWon?.Invoke();
        else
            OnGameLost?.Invoke();
    }

    public void OnGamePhantomScoreChange(Statistics statistics)
    {
        OnPhantomScoreChange?.Invoke(statistics.ScoreDescription);
        OnPhantomScorePercentageChange?.Invoke(statistics.Percentage);
    }

    public void OnGameGooScoreChange(Statistics statistics)
    {
        if (statistics.IsThresholdMet) OnGameOver(false);
        OnGooScoreChange?.Invoke(statistics.ScoreDescription);
        OnGooPercentageScoreChange?.Invoke(statistics.Percentage);
    }

    public void OnPhantoDefeatCountChange(Statistics statistics)
    {
        if (statistics.IsThresholdMet) OnGameOver(true);
        OnPhantoScoreChange?.Invoke(statistics.ScoreDescription);
        OnPhantoPercentageScoreChange?.Invoke(statistics.Percentage);
    }
}
