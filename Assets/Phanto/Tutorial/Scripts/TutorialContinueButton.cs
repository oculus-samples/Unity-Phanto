// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Haptics;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple script to handle button events in Tutorial scene
/// </summary>
public class TutorialContinueButton : MonoBehaviour
{
    private const string RESTART_SCENE = "TutorialScene";

    [Header("Page configuration")] [SerializeField]
    private float animationTime = 1.0f;

    [Space(10)] [Header("Button configuration")] [SerializeField]
    private float showButtonTime = 5.0f;

    [SerializeField] private float blinkButtonTime = 0.5f;
    [SerializeField] private float scaleButtonMultiplier = 0.5f;
    [SerializeField] private GameObject continueButton;

    [Header("Button Restart Action")] [SerializeField]
    private bool useButtonAction;

    [SerializeField] private OVRInput.RawButton restartGameButton;
    private float currentAnimTime;
    private float currentButtonTime;

    [SerializeField] private HapticCollection hapticCollection;

    [SerializeField] private Controller hapticController = Controller.Right;

    private bool hapticsAvailable = false;

    private void Start()
    {
        InitialConfiguration();

        hapticsAvailable = hapticCollection != null;
    }

    private bool _hapticPlayed = false;

    private const string WINDOW_OPEN_CLIP = "WindowOpen";

    private void Update()
    {
        if (currentAnimTime < animationTime)
        {
            currentAnimTime += Time.deltaTime;
            var t = currentAnimTime / animationTime;
            transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * scaleButtonMultiplier, t);

            if (!_hapticPlayed && hapticsAvailable)
            {
                // start a haptic effect when the scale animation starts.
                _hapticPlayed = true;

                if (hapticCollection.TryGetPlayer(WINDOW_OPEN_CLIP, out var player))
                {
                    player.Play(hapticController);
                }
                else
                {
                    Debug.LogWarning($"No player found for clip name: {WINDOW_OPEN_CLIP}");
                }
            }
        }
        else
        {
            _hapticPlayed = false;
            currentButtonTime += Time.deltaTime;
            if (currentButtonTime >= showButtonTime)
            {
                var currentBlinkTime = currentButtonTime - showButtonTime;
                if (currentBlinkTime > blinkButtonTime)
                {
                    currentButtonTime = showButtonTime;
                    continueButton.SetActive(!continueButton.activeSelf);
                }

                CheckButtonAction();
            }
        }
    }

    private void OnEnable()
    {
        InitialConfiguration();
    }

    #region private functions

    private void InitialConfiguration()
    {
        transform.localScale = Vector3.zero;
        currentAnimTime = 0;
        currentButtonTime = 0;
        continueButton.SetActive(false);
    }

    private void CheckButtonAction()
    {
        if (useButtonAction && (OVRInput.GetDown(restartGameButton) || Input.GetKeyDown(TutorialManager.ACTION_KEY)))
        {
            SceneManager.LoadSceneAsync(RESTART_SCENE);
        }
    }

    #endregion

    #region public functions

    public void SetAnimationTime(float newTime)
    {
        if (newTime > 0) animationTime = newTime;
    }

    public void SetButtonBlinkTime(float newTime)
    {
        if (newTime > 0) blinkButtonTime = newTime;
    }

    public void SetButtonScale(float newScale)
    {
        if (newScale > 0) scaleButtonMultiplier = newScale;
    }

    public bool GetButtonActive()
    {
        return currentButtonTime >= showButtonTime;
    }

    #endregion
}
