// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using Phanto.Audio.Scripts;
using UnityEngine;
using Oculus.Haptics;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>
/// Controls the triggering of a Polterblast.
/// </summary>
public class PolterblastTrigger : MonoBehaviour
{
    private static readonly Dictionary<Object, PolterblastTrigger> triggerDictionary =
        new Dictionary<Object, PolterblastTrigger>();

    private static readonly int ActivateID = Animator.StringToHash("ACTIVATE");
    private static float _accumulatedDamage = 0.0f;
    private static float _accumulatedSplash = 0.0f;

    [SerializeField] private PhantoPolterblastSfxBehavior hoseSfx;

    [SerializeField] private Animator hoseAnimator;
    [SerializeField] private Animator handleAnimator;

    [SerializeField] private ParticleSystem ectoParticleSystem;
    [SerializeField] private bool automaticEnabled;

    [SerializeField] private OVRInput.RawButton triggerButton;

    [SerializeField] private bool playHaptics = true;
    [SerializeField] private bool playAnimations = true;

    [SerializeField] private HapticClip hapticClip;

    [SerializeField] private HapticCollection hapticCollection;

    [SerializeField] private float hapticClipPlayerAmplitude = 1.0f;
    [SerializeField, Range(1, 10)] private float maxPlayerAmplitudeScale = 5.0f;
    [SerializeField, Range(0, 2)] private float splashScale = 0.5f;

    private HapticClipPlayer _hapticClipPlayer;
    private Controller _rightHand = Controller.Right;

    private bool _hapticsActive;
    private bool _triggerPulled;
    private Coroutine _hapticLoopCoroutine;

    private Transform _transform;
    private float _lastFrameDamage = 0;
    private float _lastFrameSplash = 0;

    public bool AutomaticEnabled
    {
        get => automaticEnabled;
        set => automaticEnabled = value;
    }

    public ParticleSystem EctoParticleSystem => ectoParticleSystem;
    public Vector3 Position => transform.position;

    private void Awake()
    {
        _transform = transform;

        if (playHaptics)
        {
            // Initialize haptic clip player with the clip from the inspector
            _hapticClipPlayer = new HapticClipPlayer(hapticClip);
            _hapticClipPlayer.amplitude = hapticClipPlayerAmplitude;
        }
    }

    private void Start()
    {
        if (playAnimations)
        {
            if (hoseAnimator != null) hoseAnimator.SetBool(ActivateID, false);
            handleAnimator.SetBool(ActivateID, false);
        }
    }

    private void OnEnable()
    {
        if (!triggerDictionary.TryAdd(ectoParticleSystem.gameObject, this))
        {
            Debug.LogWarning("double registering particle system");
        }
    }

    private void OnDisable()
    {
        StopHapticLoop();

        triggerDictionary.Remove(ectoParticleSystem.gameObject);
    }

    private void OnDestroy()
    {
        _hapticClipPlayer?.Dispose();
    }

    private void Update()
    {
        if (OVRInput.Get(triggerButton) || automaticEnabled)
            _triggerPulled = true;
        else
            _triggerPulled = false;

        if (_triggerPulled)
        {
            Play();
        }

        else
        {
            if (_hapticsActive && automaticEnabled == false)
            {
                _hapticsActive = false;
                StopHapticLoop();
            }

            StopHose();

            if (hoseSfx.isOn) hoseSfx.StopSfx();
        }

        if (_hapticsActive)
        {
            ProcessHapticFrequency(_hapticClipPlayer);
            ProcessHapticAmplitude(_hapticClipPlayer);
        }

        var delta = Time.deltaTime;
        _lastFrameDamage = Mathf.Lerp(_lastFrameDamage, 0, delta);
        _lastFrameSplash = Mathf.Lerp(_lastFrameSplash, 0, delta);
    }

    private void ProcessHapticFrequency(HapticClipPlayer player)
    {
        // Slightly modulate frequency during playback
        // TODO IDEA: modulate the frequency when the right hand is moved?
        player.frequencyShift = Random.Range(-0.25f, 0.25f);
    }

    private void ProcessHapticAmplitude(HapticClipPlayer player)
    {
        // Increase the amplitude based on how much damage done this frame.
        _lastFrameSplash = Mathf.Clamp(_lastFrameSplash + _accumulatedSplash * splashScale, 0, 5);
        _accumulatedSplash = 0;

        _lastFrameDamage = Mathf.Clamp(_lastFrameDamage + _accumulatedDamage, 0, 5);
        _accumulatedDamage = 0;

        var scale = MathUtils.Remap(0, 5, 1, maxPlayerAmplitudeScale, Mathf.Max(_lastFrameDamage, _lastFrameSplash));

        // scale the haptic player's amplitude by how much damage we dealt.
        player.amplitude = hapticClipPlayerAmplitude * scale;

        // XRGizmos.DrawString($"{_lastFrameDamage:F1}\n{_lastFrameSplash:F1}", _transform.position, _transform.rotation, Color.green, 0.05f);
    }

    private void StartHapticLoop()
    {
        if (!playHaptics) return;

        if (_hapticLoopCoroutine != null)
        {
            StopCoroutine(_hapticLoopCoroutine);
        }

        _hapticLoopCoroutine = StartCoroutine(HapticLoopCoroutine());
    }

    private IEnumerator HapticLoopCoroutine()
    {
        // pick a random start effect.
        var startEffect = hapticCollection.GetRandomPlayer();
        startEffect.Play(_rightHand);

        // wait until effect is done.
        yield return new WaitForSeconds(startEffect.clipDuration);

        // Set looping to true
        _hapticClipPlayer.isLooping = true;

        // Play the looping clip
        _hapticClipPlayer.Play(_rightHand);
    }

    private void StopHapticLoop()
    {
        if (_hapticLoopCoroutine != null)
        {
            StopCoroutine(_hapticLoopCoroutine);
            _hapticLoopCoroutine = null;
        }

        if (_hapticClipPlayer == null)
        {
            return;
        }

        _hapticClipPlayer.isLooping = false;
        _hapticClipPlayer.Stop();
    }

    private void StartHose()
    {
        if (ectoParticleSystem.isPlaying == false) ectoParticleSystem.Play(true);

        if (playAnimations)
        {
            if (hoseAnimator != null) hoseAnimator.SetBool(ActivateID, true);
            handleAnimator.SetBool(ActivateID, true);
        }
    }

    private void StopHose()
    {
        ectoParticleSystem.Stop(true);

        if (playAnimations)
        {
            if (hoseAnimator != null) hoseAnimator.SetBool(ActivateID, false);
            handleAnimator.SetBool(ActivateID, false);
        }
    }

    private void Play()
    {
        StartHose();

        if (playHaptics && !_hapticsActive)
        {
            _hapticsActive = true;
            StartHapticLoop();
        }

        if (!hoseSfx.loopSrc.isPlaying) hoseSfx.StartSfx();
    }

    public void GrabHandle()
    {
        StartHose();
    }

    public void ReleaseHandle()
    {
        StopHose();
    }
    /// <summary>
    /// Used to provide haptic feedback when you damage an enemy
    /// </summary>
    public static void DamageNotification(float damage, Vector3 position, Vector3 normal)
    {
        _accumulatedDamage += damage;
    }

    /// <summary>
    /// Used to provide haptic feedback when you spray goo
    /// </summary>
    public static void SplashHitNotification()
    {
        _accumulatedSplash++;
    }

    public static bool TryGetPolterblaster(Object other, out PolterblastTrigger trigger)
    {
        return triggerDictionary.TryGetValue(other, out trigger);
    }
}
