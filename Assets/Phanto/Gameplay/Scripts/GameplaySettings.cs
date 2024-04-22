// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OVRSimpleJSON;
using Phanto;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "Phanto/GameplaySettings", order = 1)]
public class GameplaySettings : ScriptableObject
{
    [SerializeField]
    private List<WaveSettings> waveSettings;

    private GameplaySettingsManager _manager;

    // Individual settings list
    public PhantoSetting[] PhantoSettingsList =>  waveSettings.Select((setting) => setting.phantoSetting).ToArray();
    public PhantomSetting[] PhantomSettingsList =>  waveSettings.Select((setting) => setting.phantomSetting).ToArray();
    public GuiSettings[] GuiSettingsList =>  waveSettings.Select((setting) => setting.guiSettings).ToArray();

    // Current settings accessor
    public WaveSettings CurrentWaveSettings =>  waveSettings[_manager.Wave];
    public PhantoSetting CurrentPhantoSetting => PhantoSettingsList[_manager.Wave];
    public PhantomSetting CurrentPhantomSetting => PhantomSettingsList[_manager.Wave];
    public GuiSettings CurrentGuiSetting => GuiSettingsList[_manager.Wave];

    public WaveSettings CurrentWave => waveSettings[_manager.Wave];

    public int MaxWaves => waveSettings.Count;

    public enum WinCondition
    {
        DefeatPhanto,
        DefeatAllPhantoms
    }

    public class GameSetting
    {
        [Tooltip("Whether this setting will apply to the wave")]
        public bool isEnabled = true;
    }

    [System.Serializable]
    public class WaveSettings
    {
        [Tooltip("This wave win conditions")]
        public WinCondition winCondition = WinCondition.DefeatPhanto;

        [Header("Individual settings")]
        public GuiSettings guiSettings;
        public PhantoSetting phantoSetting;
        public PhantomSetting phantomSetting;
    }

    [System.Serializable]
    public class PhantoSetting:GameSetting
    {
        [Header("Phanto General settings")]
        [Tooltip("The amount of damage to apply to the enemy.")]
        public float splashDamage = 0.03f;

        [Tooltip("Splash amount")]
        public float splashAmount = 0.4f;

        [Tooltip("Sets the speed of the projectile.")]
        public Vector2 gooBallSpeed = new Vector2(2,5);

        [Header("Phanto Behaviour settings")]
        public PhantoBehaviour.Nova.NovaSettings novaSettings;
        public PhantoBehaviour.SpitGooBall.SpitGooBallSettings spitGooBallSettings;
        public PhantoBehaviour.Pain.PainSettings painSettings;
        public PhantoBehaviour.GoEthereal.GoEtherealSettings goEtherealSettings;
        public PhantoBehaviour.Dodge.DodgeSettings dodgeSettings;
        public PhantoBehaviour.Roam.RoamSettings roamSettings;
    }

    [System.Serializable]
    public class PhantomSetting:GameSetting
    {
        [Tooltip("Phantom movement speed")]
        public float Speed = 1;

        [Tooltip("Phantom movement flee speed multiplier = ")]
        public float FleeSpeedMultiplier = 3.0f;

        [Tooltip("Max number of phantoms to spawn")]
        public int Quantity = 4;

        [Tooltip("Phantom spawn rate")]
        public float SpawnRate = 2;
        public int SpawnPoints = 1;


        [Tooltip("Phantom attack delay")]
        public float AttackDelay = 1.0f;
        public float RangedAttackDelay = 1.5f;

        public override string ToString()
        {
            var json = new JSONObject
            {
                [nameof(Quantity)] = Quantity,
                [nameof(SpawnRate)] = SpawnRate,
                [nameof(AttackDelay)] = AttackDelay,
                [nameof(RangedAttackDelay)] = RangedAttackDelay
            };

            return json.ToString(2);
        }

        public enum BlasterHands
        {
            LeftHand,
            RightHand,
            Both
        }

        public BlasterHands blasterHands = BlasterHands.LeftHand;
    }

    [System.Serializable]
    public class GuiSettings:GameSetting
    {
        [Tooltip("The wave title")]
        public string WaveTitle = "Wave X";
        [Tooltip("The subtitle for the wave objective")]
        public string WaveObjective = "Sample objective";
        [Tooltip("Wave popup delay time")]
        public float popupDelayShowTime = 5;
        [Tooltip("Phanto wave time")]
        public float showBackPhantoWaveTime;
    }

    public void SetManager(GameplaySettingsManager gameplaySettingsManager)
    {
        _manager = gameplaySettingsManager;
    }

    public WaveSettings GetWaveSettings(int wave)
    {
        return waveSettings[wave];
    }
}
