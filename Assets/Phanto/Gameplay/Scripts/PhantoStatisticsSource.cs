// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using Phantom;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;

/// <summary>
/// Source for Phanto statistics
/// </summary>
public class PhantoStatisticsSource : StatisticsSource
{
    [SerializeField] private GameplaySettingsManager settingsManager;

    public UnityEvent<Statistics> OnPhantomsAlert;
    public UnityEvent<Statistics> OnGoosAlert;
    public UnityEvent<Statistics> OnPhantoAlert;
    private Statistics _activeGoosStats;
    private Statistics _activePhantomsStats;
    private Statistics _activePhantoStats;
    private bool _isReady;

    private List<Statistics> _statistics;

    private GameplayManager _gameplayManager;

    private void Awake()
    {
        _gameplayManager = GetComponent<GameplayManager>();

        Assert.IsNotNull(_gameplayManager);
        Assert.IsNotNull(settingsManager);
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => PhantomManager.Instance != null);

        _statistics = new List<Statistics>();
        _activePhantomsStats = new Statistics("ACTIVE_PHANTOMS", PhantomManager.Instance.MaxPhantoms);
        _activePhantomsStats.OnThresholdMetAlert += OnActivePhatomsAlert;
        _activePhantomsStats.OnValueChanged += OnActivePhatomsAlert;
        _statistics.Add(_activePhantomsStats);

        _activeGoosStats = new Statistics("ACTIVE_GOOS", _gameplayManager.MaxGoos, _gameplayManager.MaxGoos);
        _activeGoosStats.OnThresholdMetAlert += OnActiveGoosAlert;
        _activeGoosStats.OnValueChanged += OnActiveGoosAlert;
        _statistics.Add(_activeGoosStats);

        _activePhantoStats = new Statistics("ACTIVE_PHANTO", GameplaySettingsManager.Instance.gameplaySettings.MaxWaves - 1, GameplaySettingsManager.Instance.gameplaySettings.MaxWaves - 1);
        _activePhantoStats.OnThresholdMetAlert += OnActivePhantoAlert;
        _activePhantoStats.OnValueChanged += OnActivePhantoAlert;
        _activePhantoStats.CurrentValue = settingsManager.Wave;
        settingsManager.OnWaveAdvance += () => _activePhantoStats.CurrentValue += 1;
        _statistics.Add(_activePhantoStats);

        _isReady = true;
    }

    private void Update()
    {
        if (_isReady)
        {
            _activePhantomsStats.CurrentValue = PhantomManager.Instance.ActivePhantoms.Count;
            _activeGoosStats.CurrentValue = PhantoGoo.ActiveGoos.Count;
        }
    }

    private void OnActiveGoosAlert(Statistics activeGoosStats)
    {
        OnGoosAlert?.Invoke(activeGoosStats);
    }

    private void OnActivePhatomsAlert(Statistics activePhantomsStats)
    {
        OnPhantomsAlert?.Invoke(activePhantomsStats);
    }

    private void OnActivePhantoAlert(Statistics activePhantoStatistics)
    {
        OnPhantoAlert?.Invoke(activePhantoStatistics);
    }


    public override List<Statistics> StatisticsList()
    {
        return _statistics;
    }

    public override bool IsReady()
    {
        return _isReady;
    }
}
