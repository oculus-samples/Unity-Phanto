// Copyright (c) Meta Platforms, Inc. and affiliates.

using Phanto.Audio.Scripts;
using UnityEngine;

/// <summary>
/// Controls the triggering of a Polterblast.
/// </summary>
public class PolterblastTrigger : MonoBehaviour
{
    public PhantoPolterblastSfxBehavior hoseSfx;

    public bool primary = true;
    public float onRate = 10;

    public Animator hoseAnimator;
    public Animator handleAnimator;

    public ParticleSystem ectoParticleSystem;
    public bool automaticEnabled;

    [SerializeField] private OVRInput.RawButton _triggerButton;

    [SerializeField] private bool playHaptics = true;
    [SerializeField] private bool playAnimations = true;
    private readonly float hapticCycle = 2;

    private bool hapticsActive;
    private float hapticTimer;

    private bool triggerPulled;

    private void Start()
    {
        if (playAnimations)
        {
            if (hoseAnimator != null) hoseAnimator.SetBool("ACTIVATE", false);
            handleAnimator.SetBool("ACTIVATE", false);
        }
    }

    private void Update()
    {
        if (OVRInput.Get(_triggerButton) || automaticEnabled)
            triggerPulled = true;
        else
            triggerPulled = false;

        if (triggerPulled)
        {
            Play();
        }

        else
        {
            if (hapticsActive && automaticEnabled == false)
            {
                hapticsActive = false;
                PlayHapticEvent(0.0f, 0.0f);
            }

            StopHose();

            if (hoseSfx.isOn) hoseSfx.StopSfx();
        }
    }

    public void PlayHapticEvent(float frequency, float amplitude)
    {
        if (!playHaptics) return;

        OVRInput.SetControllerVibration(frequency, amplitude, OVRInput.Controller.RTouch);
    }

    public void StartHose()
    {
        if (ectoParticleSystem.isPlaying == false) ectoParticleSystem.Play(true);

        if (playAnimations)
        {
            if (hoseAnimator != null) hoseAnimator.SetBool("ACTIVATE", true);
            handleAnimator.SetBool("ACTIVATE", true);
        }
    }

    public void StopHose()
    {
        ectoParticleSystem.Stop(true);

        if (playAnimations)
        {
            if (hoseAnimator != null) hoseAnimator.SetBool("ACTIVATE", false);
            handleAnimator.SetBool("ACTIVATE", false);
        }
    }

    public void Play()
    {
        StartHose();

        if (!hapticsActive)
        {
            hapticsActive = true;
            PlayHapticEvent(0.25f, 0.35f);
        }
        else
        {
            hapticTimer += Time.deltaTime;
            if (hapticTimer >= hapticCycle)
            {
                PlayHapticEvent(0.25f, 0.35f);
                hapticTimer = 0;
            }
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
}
