// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Manages statistics
/// </summary>
public class StatisticsManager : MonoBehaviour
{
    public static StatisticsManager Instance;
    [SerializeField] private StatisticsSource StatisticsSource;

    private readonly Dictionary<string, Statistics> _statisticsMap = new();

    private void Awake()
    {
        Instance = this;
        Assert.IsNotNull(StatisticsSource);
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => StatisticsSource.IsReady());
        foreach (var statistics in StatisticsSource.StatisticsList()) AddEntry(statistics);
    }

    public void AddEntry(string name, float upperBound, float threshold = 0, float startValue = 0)
    {
        _statisticsMap.Add(name, new Statistics(name, upperBound, threshold, startValue));
    }

    public void AddEntry(Statistics statistics)
    {
        _statisticsMap.Add(statistics.Description, statistics);
    }

    public void GetEntry(string name, out Statistics value)
    {
        var success = _statisticsMap.TryGetValue(name, out var val);
        if (success)
        {
            value = val;
        }
        else
        {
            Debug.LogError($"Wrong Statistic field name: {name}");
            value = null;
        }
    }
}

/// <summary>
/// Object to Manages statistics
/// </summary>
[Serializable]
public class Statistics
{
    public string Description;

    // Value upper bound
    private readonly float _upperBound;

    // Current statistic value
    private float _currentValue;

    // Value alert threshold
    private float _threshold = float.MaxValue;
    public Action<Statistics> OnThresholdMetAlert;
    public Action<Statistics> OnValueChanged;

    public Statistics(string name, float upperBound, float threshold = float.MaxValue, float startValue = 0)
    {
        Description = name;
        _currentValue = startValue;
        _upperBound = upperBound;
        _threshold = threshold;
    }

    public string ScoreDescription => $"{CurrentValue}/{_upperBound}({Percentage}%)";
    public float Percentage => _currentValue / _upperBound;
    public bool IsThresholdMet => _currentValue > _threshold;

    public float CurrentValue
    {
        get => _currentValue;
        set
        {
            _currentValue = value;
            OnValueChanged?.Invoke(this);
            if (_currentValue > _threshold)
            {
                Debug.Log($"Value threshold met for statistic {Description}");
                OnThresholdMetAlert?.Invoke(this);
            }
        }
    }

    public override string ToString()
    {
        return $"Statistics for: {Description}: (Value:{CurrentValue}/{_upperBound}, alert at {_threshold})";
    }
}
