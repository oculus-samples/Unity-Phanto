// Copyright (c) Meta Platforms, Inc. and affiliates.

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Phanto.Audio.Scripts;

/// <summary>
/// Manages statistics UI element
/// </summary>
public class UIStatisticsManager : MonoBehaviour
{
    [Header("UI Elements 2D")] [SerializeField]
    private GameObject mainPanel;

    [SerializeField] private TextMeshProUGUI gooCounterText;
    [SerializeField] private Image gooIcon;

    [Space(10)] [Header("UI Elements Hand")] [SerializeField]
    private GameObject mainPanelHand;

    [SerializeField] private TextMeshProUGUI gooCounterTextHand;
    [SerializeField] private Image gooIconHand;
    [SerializeField] private bool useHand = true;

    [Space(10)] [Header("Alert Levels configuration")] [SerializeField]
    private float gooAlertStart = 0.2f;

    [SerializeField] private float gooAlertLow = 0.3f;
    [SerializeField] private float gooAlertMid = 0.5f;
    [SerializeField] private float gooAlertMax = 0.7f;
    [SerializeField] private float gooAlertEnd = 0.8f;

    [Space(10)] [Header("Blink Anim configuration")] [SerializeField]
    private float gooAlertBlinkLevel = 2f;

    [SerializeField] private float gooAlertBlinkTime = 0.15f;
    [Space(10)]

    [Header("Warning sfx")]
    [SerializeField] private PhantoRandomOneShotSfxBehavior alertSfx;

    [Space(10)] [Header("Debug configuration")] [SerializeField]
    private bool debugInfo;

    private int _alertLevel = -1;
    private bool _blinkCurrentStatus;

    private float _blinkCurrentTime;

    private bool _lose;

    private void Awake()
    {
        gooCounterText.gameObject.SetActive(false);
        gooCounterTextHand.gameObject.SetActive(false);
        gooIconHand.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_alertLevel >= gooAlertBlinkLevel)
        {
            _blinkCurrentTime += Time.deltaTime;

            if (_blinkCurrentTime > gooAlertBlinkTime * (4.00f / (_alertLevel + 1)))
            {
                _blinkCurrentTime = 0;
                _blinkCurrentStatus = !_blinkCurrentStatus;

                if (_alertLevel == 2) {
                    alertSfx.pitchMin = 1.5f;
                    alertSfx.pitchMax = 1.5f;
                } else if (_alertLevel == 3)
                {
                    alertSfx.pitchMin = 1.7f;
                    alertSfx.pitchMax = 1.7f;
                } 
                {
                    alertSfx.pitchMin = 1f;
                    alertSfx.pitchMax = 1f;
                }

                if (_blinkCurrentStatus) 
                    alertSfx.PlaySfx();
            }
        }
        else
        {
            _blinkCurrentStatus = true;
        }

        if (useHand)
            gooCounterTextHand.gameObject.SetActive(_blinkCurrentStatus);
        else
            gooCounterText.gameObject.SetActive(_blinkCurrentStatus);
    }

    private void HideMainPanel()
    {
        FindAnyObjectByType<GameplayManager>().OnGameOver(false);
        _lose = true;
        mainPanel.SetActive(false);
    }

    /// <summary>
    /// Updates statistics UI
    /// </summary>
    public void UpdateGooCounter(float value)
    {
        var iconColor = Color.white;

        //default values
        var color = "#FEFF68";
        var msg = "";
        _alertLevel = -1;

        // per level config.
        if (value >= gooAlertStart && value < gooAlertLow)
        {
            color = "#FEFF68";
            _alertLevel = 0;
            msg = "<b>WARNING</b>\nGOO IN YOUR ROOM";
        }

        if (value >= gooAlertLow && value < gooAlertMid)
        {
            color = "#FEFF68";
            _alertLevel = 1;
            msg = "<b>DANGEROUS</b>\nGOO LEVEL IS RISING";
        }

        if (value >= gooAlertMid && value < gooAlertMax)
        {
            color = "#FF687F";
            _alertLevel = 2;
            msg = "<b>ALERT</b>\nGOO LEVEL IS TOO HIGH";
        }

        if (value >= gooAlertMax && value < gooAlertEnd)
        {
            color = "#FF687F";
            _alertLevel = 3;
            msg = "<b>PANIC</b>\nFOCUS ON CLEAN GOO";
        }

        //icon values
        bool iconVisible;
        string iconText;
        if (_alertLevel >= gooAlertBlinkLevel)
        {
            if (debugInfo)
                iconText = $"<color={color}>GOO: {value}</color>";
            else
                iconText = $"<color={color}>{msg}</color>";
            iconVisible = true;
            ColorUtility.TryParseHtmlString(color, out iconColor);
        }
        else
        {
            iconText = "";
            iconVisible = false;
        }

        if (useHand)
        {
            gooCounterTextHand.text = iconText;
            gooIconHand.color = iconColor;
            gooIconHand.gameObject.SetActive(iconVisible);
        }
        else
        {
            gooCounterText.text = iconText;
            gooIcon.color = iconColor;
            gooIcon.gameObject.SetActive(iconVisible);
        }

        if (value >= gooAlertEnd && !_lose) HideMainPanel();
    }

    public float GetAlertLevel()
    {
        return _alertLevel;
    }
}
