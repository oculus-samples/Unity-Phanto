// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using Oculus.Haptics;
using Phanto.Audio.Scripts;
using Phantom;
using PhantoUtils.VR;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the Tutorial Pages
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public const KeyCode ACTION_KEY = KeyCode.Alpha1;
    private const KeyCode ACTION2_KEY = KeyCode.Alpha2;

    private const string WINDOW_CLOSE_CLIP = "WindowClose";
    private const string PLAYERPREF_TURORIAL_KEY = "Tutorial";
    private const string GAME_SCENE_NAME = "GameScene";

    [Header("Global configuration")] [SerializeField]
    private float startDelayTime = 1.5f;

    [SerializeField] private float changeDelayTime = 1.0f;
    [SerializeField] private TutorialPageData[] tutorialPages;

    [Space(10)] [Header("Page configuration")] [SerializeField]
    private float overrideEnableAnimTime = 0.15f;

    [SerializeField] private float overrideEnableAnimScale = 0.5f;
    [SerializeField] private float overrideButtonBlinkTime = 0.2f;

    [Space(10)] [Header("Audio configuration")] [SerializeField]
    private PhantoGooSfxManager soundManager;

    [SerializeField] private AudioSource showPageSound;
    [SerializeField] private AudioSource hidePageSound;
    [SerializeField] private AudioSource tutorialCompletedSound;

    [Space(10)] [Header("Debug configuration")] [SerializeField]
    private bool showDebug;

    [SerializeField] private GameObject modalDebug;
    [SerializeField] private TextMeshProUGUI phantomsDebugModal;
    [SerializeField] private TextMeshProUGUI completeDebugModal;

    [SerializeField] private HapticCollection uiHaptics;

    [SerializeField] private UnityEvent<int, bool> showTutorialPage;

    private int currentPage = -1;
    private int currentTries;
    private bool isCompleted;
    private bool pressed = true;

    private Transform head;

    private TutorialPhantomManager _phantomManager;
    private GameplaySettingsManager _gameplaySettingsManager;

    private void Awake()
    {
        isCompleted = CheckTutorialCompleted();
    }

    private IEnumerator Start()
    {
        do
        {
            _phantomManager = PhantomManager.Instance as TutorialPhantomManager;
            yield return null;
        } while (_phantomManager == null);

        CleanTutorial();
        // Debug ----------
        if (modalDebug) modalDebug.SetActive(showDebug);
        // ----------

        while (CameraRig.Instance == null)
        {
            yield return null;
        }

        head = CameraRig.Instance.CenterEyeAnchor;

        PhantoGooSfxManager.Instance.StartTutorialMusic();

        _gameplaySettingsManager = GameplaySettingsManager.Instance;
        Assert.IsNotNull(_gameplaySettingsManager);
    }

    private void Update()
    {
        if (IsVisiblePage())
        {
            var page = tutorialPages[currentPage];
            if (!page.waitForPhantoms)
            {
                // Regular tutorial page
                if (page.pageUI.GetComponent<TutorialContinueButton>().GetButtonActive())
                {
                    if (OVRInput.GetDown(page.actionButton) || Input.GetKeyDown(ACTION_KEY))
                    {
                        if (!pressed)
                        {
                            PlayHaptic(WINDOW_CLOSE_CLIP, page.actionButton);

                            if (page.pageUI.activeSelf) page.pageUI.SetActive(false);
                            currentTries++;
                            // Debug ----
                            if (showDebug) completeDebugModal.text = currentTries.ToString();
                            // ----------
                            if (currentTries > page.completeTries)
                            {
                                pressed = true;
                                SetNextTutorial();
                            }
                        }
                    }
                    else if (pressed)
                    {
                        pressed = false;
                    }

                    // Only for Complete Modal 00
                    if ((OVRInput.GetDown(page.actionButton2) || Input.GetKeyDown(ACTION2_KEY)) && isCompleted)
                    {
                        PlayHaptic(WINDOW_CLOSE_CLIP, page.actionButton2);

                        pressed = true;
                        OnTutorialComplete();
                    }
                }
            }
            else
            {
                // Waiting for Phatoms hidden page
                if (_phantomManager.MobTutorialComplete)
                {
                    page.waitForPhantoms = false;
                    SetNextTutorial();
                }
                else if (phantomsDebugModal)
                {
                    // Debug ----
                    if (showDebug) phantomsDebugModal.text = $"{_phantomManager.ActivePhantoms.Count:00}";
                    // ----------
                }
            }
        }
    }

    #region public functions

    public void OnSceneDataLoaded()
    {
        if (currentPage == -1)
        {
            if (isCompleted)
                currentPage = 0;
            else
                currentPage = 1;
            StartCoroutine(SetTutorialDelayed(startDelayTime));
        }
    }

    #endregion

    [Serializable]
    public struct TutorialPageData
    {
        public string id;
        public GameObject pageUI;
        public OVRInput.RawButton actionButton;
        public OVRInput.RawButton actionButton2;
        public bool visible;
        public bool waitForPhantoms;
        public int completeTries;
    }

    #region private functions

    private void CleanTutorial()
    {
        currentPage = -1;
        pressed = true;
        foreach (var page in tutorialPages)
            if (page.pageUI != null)
            {
                page.pageUI.SetActive(false);
                page.pageUI.GetComponent<TutorialContinueButton>().SetAnimationTime(overrideEnableAnimTime);
                page.pageUI.GetComponent<TutorialContinueButton>().SetButtonScale(overrideEnableAnimScale);
                page.pageUI.GetComponent<TutorialContinueButton>().SetButtonBlinkTime(overrideButtonBlinkTime);
            }

        ActivateMobTutorial(false);
    }

    private void SetNextTutorial()
    {
        currentTries = 0;
        if (currentPage < tutorialPages.Length)
        {
            SetTutorialPage(false);
            currentPage++;
            StartCoroutine(SetTutorialDelayed(changeDelayTime));
        }
    }

    private void SetTutorialPage(bool visible)
    {
        if (IsValidPage())
        {
            if (tutorialPages[currentPage].pageUI != null)
            {
                tutorialPages[currentPage].pageUI.SetActive(visible);
                if (visible)
                    PlaySound(showPageSound);
                else
                    PlaySound(hidePageSound);
            }

            var waveSettings = _gameplaySettingsManager.GetWaveSettings(currentPage);

            tutorialPages[currentPage].visible = visible;
            ActivateMobTutorial(tutorialPages[currentPage].waitForPhantoms && visible, waveSettings.winCondition);

            // Stop music at the end of the Tutorial
            if (currentPage + 1 == tutorialPages.Length)
            {
                // Stop music
                if (CheckSoundManager())
                {
                    soundManager.StopMusic(true);
                    tutorialCompletedSound.Play();
                }

                return;
            }

            showTutorialPage?.Invoke(currentPage, visible);
        }
        else if (visible && currentPage >= tutorialPages.Length)
        {
            // Tutorial completed
            OnTutorialComplete();
        }
    }

    private void PlaySound(AudioSource sound)
    {
        if (sound) sound.Play();
    }

    private bool IsValidPage()
    {
        return currentPage >= 0 && currentPage < tutorialPages.Length;
    }

    private bool IsVisiblePage()
    {
        if (IsValidPage()) return tutorialPages[currentPage].visible;
        return false;
    }

    private IEnumerator SetTutorialDelayed(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        SetTutorialPage(true);
    }

    private void ActivateMobTutorial(bool visible,
        GameplaySettings.WinCondition winCondition = GameplaySettings.WinCondition.DefeatAllPhantoms)
    {
        _phantomManager.ActivateMobTutorial(visible, winCondition);
    }

    private void OnTutorialComplete()
    {
        if (CheckSoundManager()) soundManager.StopMusic(true);
        // save in playerpref
        PlayerPrefs.SetInt(PLAYERPREF_TURORIAL_KEY, 1);
        // load game scene
        SceneManager.LoadSceneAsync(GAME_SCENE_NAME);
    }

    private bool CheckTutorialCompleted()
    {
        // check if tutorial has been completed
        return PlayerPrefs.GetInt(PLAYERPREF_TURORIAL_KEY) == 1;
    }

    private bool CheckSoundManager()
    {
        if (!soundManager) soundManager = FindAnyObjectByType<PhantoGooSfxManager>();
        return soundManager != null;
    }

    private void PlayHaptic(string clip, OVRInput.RawButton button)
    {
        if (uiHaptics.TryGetPlayer(clip, out var player))
        {
            var controller = Controller.Right;

            if ((button & (OVRInput.RawButton.LHandTrigger
                           | OVRInput.RawButton.LIndexTrigger
                           | OVRInput.RawButton.X
                           | OVRInput.RawButton.Y
                           | OVRInput.RawButton.Start
                           | OVRInput.RawButton.LThumbstick)) != 0)
            {
                controller = Controller.Left;
            }

            player.Play(controller);
        }
    }

    #endregion
}
