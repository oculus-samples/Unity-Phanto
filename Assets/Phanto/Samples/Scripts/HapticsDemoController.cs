// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Haptics;
using TMPro;
using UnityEngine;

public class HapticsDemoController : MonoBehaviour
{
    private const string Instructions1 = "Squeeze the hand grip to start the haptic effect.";
    private const string Instructions2 = "* Pull trigger to increase amplitude.\n* Move thumbstick left and right to change frequency shift.\n\namp: {0:F2}\nfreq: {1:F2}";

    [SerializeField] private HapticClip loopingClip;

    [SerializeField] private OVRInput.Controller inputController = OVRInput.Controller.RTouch;

    [SerializeField] private Controller hapticController = Controller.Right;

    [SerializeField, Range(0, 5)] private float amplitudeShiftScale = 5.0f;

    [SerializeField] private TextMeshProUGUI text;

    private Transform _transform;

    private HapticClipPlayer _loopingPlayer;

    private bool _isPlaying;

    private void Awake()
    {
        _transform = transform;
        _loopingPlayer = new HapticClipPlayer(loopingClip);
        _loopingPlayer.isLooping = true;
    }

    private void OnEnable()
    {
        text.text = Instructions1;
    }

    private void OnDestroy()
    {
        _loopingPlayer?.Dispose();
    }

    private void Update()
    {
        var trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, inputController);
        var grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, inputController);
        var thumbstick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, inputController);

        var shouldPlay = grip > 0.01f;

        if (!_isPlaying && shouldPlay)
        {
            _loopingPlayer.Play(hapticController);
            _isPlaying = true;
        }
        else if (_isPlaying && !shouldPlay)
        {
            _loopingPlayer.Stop();
            _isPlaying = false;

            text.text = Instructions1;
        }

        if (!_isPlaying)
        {
            return;
        }

        var amplitude = MathUtils.Remap(0, 1, 1, amplitudeShiftScale, trigger);
        _loopingPlayer.amplitude = amplitude;

        var freqShift = Mathf.Clamp(thumbstick.x, -1, 1);
        _loopingPlayer.frequencyShift = freqShift;

        text.text = string.Format(Instructions2, amplitude, freqShift);
    }
}
