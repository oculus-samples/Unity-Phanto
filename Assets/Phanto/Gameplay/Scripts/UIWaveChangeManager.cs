// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using Oculus.Haptics;
using Phanto.Audio.Scripts;
using Phantom;
using PhantoUtils.VR;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Manages wave popup visibility and timing
/// </summary>
public class UIWaveChangeManager : MonoBehaviour
{
    [Tooltip("Wave popup reference")]
    [SerializeField] private GameObject wavePopup;

    [Tooltip("Wave popup text")]
    [SerializeField] private TextMeshProUGUI wavePanelText;

    [Tooltip("Wave max counter")]
    [SerializeField] private int _waveMaxCounter = 3;

    [Tooltip("Wave popup delay time")]
    [SerializeField] private float[] popupDelayShowTime;

    [Tooltip("Wave popup show time")]
    [SerializeField] private float popupShowTime = 5.0f;

    [Tooltip("Phanto wave time")]
    [SerializeField] private float[] showBackPhantoWaveTime;

    [SerializeField] private GameObject phantoPooFx;
    [SerializeField] private PhantoGooSfxManager soundManager;
    [SerializeField] private AudioSource wavePopupSound;

    [SerializeField] private HapticClip wavePopupHaptic;

    [SerializeField] private Controller hapticController = Controller.Both;

    [SerializeField] private HapticCollection phantoDefeatCollection;

    private GameObject _phanto;
    private bool _started;
    private bool _startMusic;
    private int _waveCounter;

    private bool _waveStart;

    private int  _showUIWave = 0;
    private int _showUIWaveMem = -1;

    private HapticClipPlayer _hapticPlayer;

    private void Awake()
    {
        wavePopup.SetActive(false);

        if (wavePopupHaptic != null)
        {
            _hapticPlayer = new HapticClipPlayer(wavePopupHaptic);
        }
    }

    private void OnDestroy()
    {
        _hapticPlayer?.Dispose();
    }

    private void LateUpdate()
    {
        if (_showUIWaveMem != _showUIWave)
        {
            _showUIWaveMem = _showUIWave;
            ShowWavePanel(_showUIWave);
        }
    }

    public void ManagePhantoBetweenWave(GameObject _phanto, bool _started)
    {
        if (!_startMusic)
            if (CheckSoundManager())
            {
                soundManager.StartMusic();
                _startMusic = true;
            }

        this._phanto = _phanto;
        this._started = _started;

        _waveStart = false;
        _waveCounter++;
        if (_waveCounter <= _waveMaxCounter)
        {
            // hide Phanto if the gameplay has been started
            if (_phanto != null && _started) ShowPhanto(false);
            // Show wave popup
            StartCoroutine(ShowAgainPhanto(_waveCounter));
        }

        if (_waveCounter > 1)
        {
            PlayPhantoDefeatHaptic();
        }
    }

    public bool GetWaveStart()
    {
        return _waveStart;
    }

    private IEnumerator ShowAgainPhanto(int waveNum)
    {
        // wait until show popup
        var showWavePopup = popupDelayShowTime[waveNum - 1];
        yield return new WaitForSeconds(showWavePopup);
        // time params
        var showBackPhanto = showBackPhantoWaveTime[waveNum - 1];
        // show wave popup
        _showUIWave = waveNum;
        yield return new WaitForSeconds(popupShowTime);
        // hide wave popup
        _showUIWave = 0;
        yield return new WaitForSeconds(showBackPhanto);
        // show Phanto if the gameplay has been started
        if (_phanto != null && _started)
        {
            var _phantomManager = FindAnyObjectByType<PhantomManager>();
            var nPos = _phantomManager.RandomPointOnFloor(_phanto.transform.position, 2.0f);
            nPos.y = CameraRig.Instance.CenterEyeAnchor.position.y;
            _phanto.transform.position = nPos;
            ShowPhanto(true);
        }

        _waveStart = true;
    }

    private void ShowWavePanel(int waveNum)
    {
        if (waveNum == 0)
        {
            wavePopup.SetActive(false);
        }
        else
        {
            wavePanelText.text = $"WAVE {waveNum}";
            wavePopup.SetActive(true);
            wavePopupSound.Play();
            _hapticPlayer?.Play(hapticController);
        }
    }

    public void ShowPhanto(bool visible)
    {
        if (_phanto != null)
        {
            _phanto.SetActive(visible);
            if (phantoPooFx)
            {
                phantoPooFx.transform.position = _phanto.transform.position;
                phantoPooFx.GetComponent<ParticleSystem>().Play();
            }

            if (CheckSoundManager()) soundManager.PlayPhantoAppearVo(_phanto.transform.position);
        }
    }

    private bool CheckSoundManager()
    {
        if (!soundManager) soundManager = FindAnyObjectByType<PhantoGooSfxManager>();
        return soundManager != null;
    }

    private void PlayPhantoDefeatHaptic()
    {
        var player = phantoDefeatCollection.GetRandomPlayer();

        player?.Play(hapticController);
    }
}
