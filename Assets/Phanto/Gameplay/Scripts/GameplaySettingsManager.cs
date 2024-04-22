// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using PhantoUtils;
using UnityEngine.Assertions;
using UnityEngine.Events;

public class GameplaySettingsManager : SingletonMonoBehaviour<GameplaySettingsManager>
{
    public GameplaySettings gameplaySettings;

    public UnityEvent<GameplaySettings.WaveSettings> OnNewWave;
    public bool WavesAvailable => _wavesAvailable;

    private bool _wavesAvailable;

    public event Action OnWaveAdvance;

    public int Wave { get; private set;}

    protected override void Awake()
    {
        base.Awake();

        Assert.IsNotNull(gameplaySettings, $"{nameof(gameplaySettings)} cannot be null.");

        var waveAdvanceManager = FindObjectOfType<UIWaveChangeManager>();
        _wavesAvailable = waveAdvanceManager != null;
        if (_wavesAvailable)
        {
            waveAdvanceManager.onNewWave.AddListener(OnWaveChange);
        }

        gameplaySettings.SetManager(this);
    }

    private void OnWaveChange()
    {
        OnNewWave?.Invoke(gameplaySettings.CurrentWaveSettings);
    }

    public void AdvanceWave()
    {
        SetWave(Wave + 1);
        OnWaveAdvance?.Invoke();
    }

    public GameplaySettings.WaveSettings GetWaveSettings(int wave = -1)
    {
        ClampWave(ref wave);

        return gameplaySettings.GetWaveSettings(wave);
    }

    public void SetWave(int wave)
    {
        ClampWave(ref wave);

        Wave = wave;
    }

    private void ClampWave(ref int wave)
    {
        if (wave < 0)
        {
            wave = 0;
        }

        if (wave >= gameplaySettings.MaxWaves)
        {
            wave = gameplaySettings.MaxWaves - 1;
        }
    }
}
