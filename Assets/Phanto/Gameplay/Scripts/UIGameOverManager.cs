// Copyright (c) Meta Platforms, Inc. and affiliates.

using Phanto.Audio.Scripts;
using TMPro;
using UnityEngine;

/// <summary>
///     Responsible for managing UI Game over pop-ups
/// </summary>
public class UIGameOverManager : MonoBehaviour
{
    [SerializeField] private GameObject winPopup;
    [SerializeField] private TextMeshProUGUI winPopupTime;
    [SerializeField] private GameObject rankingPopup;
    [SerializeField] private GameObject losePopup;
    [SerializeField] private PhantoGooSfxManager soundManager;
    [SerializeField] private AudioSource winSound;
    [SerializeField] private PhantoRandomOneShotSfxBehavior loseSound;
    private UIGameplayTimeManager uIGameplayTimeManager;

    private UIWaveChangeManager uiWaveManager;

    private void Awake()
    {
        winPopup.SetActive(false);
        losePopup.SetActive(false);
        rankingPopup.SetActive(false);
    }

    private void Start()
    {
        uiWaveManager = GetComponent<UIWaveChangeManager>();
        uIGameplayTimeManager = GetComponent<UIGameplayTimeManager>();
    }

    public void ShowWinPopup()
    {
        if (CheckSoundManager())
        {
            soundManager.StopMusic(true);
            winSound.Play();
        }

        winPopup.SetActive(true);
        losePopup.SetActive(false);
        uiWaveManager.ShowPhanto(false);
        uIGameplayTimeManager.OnEndGame();
        winPopupTime.text = uIGameplayTimeManager.GetPlayedTime();
    }

    public void ShowLosePopup()
    {
        if (CheckSoundManager())
        {
            soundManager.StopMusic();
            loseSound.PlaySfx();
        }

        winPopup.SetActive(false);
        losePopup.SetActive(true);
        uiWaveManager.ShowPhanto(false);
    }

    private bool CheckSoundManager()
    {
        if (!soundManager) soundManager = FindAnyObjectByType<PhantoGooSfxManager>();
        return soundManager != null;
    }
}
