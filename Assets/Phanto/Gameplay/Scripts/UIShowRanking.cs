// Copyright (c) Meta Platforms, Inc. and affiliates.

using TMPro;
using UnityEngine;

/// <summary>
/// Shows the ranking UI when the user presses the change game button
/// </summary>
public class UIShowRanking : MonoBehaviour
{
    [SerializeField] private OVRInput.RawButton changeGameButton;
    [SerializeField] private GameObject rankingPopup;
    [SerializeField] private TextMeshProUGUI rankingPopupText;
    [SerializeField] private UIGameplayTimeManager uIGameplayTimeManager;

    private void Update()
    {
        if (OVRInput.GetDown(changeGameButton) || Input.GetKeyDown(KeyCode.R))
        {
            rankingPopup.SetActive(true);
            rankingPopupText.text = uIGameplayTimeManager.GetRanking();
            gameObject.SetActive(false);
        }
    }
}
