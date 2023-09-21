// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Phanto;
using PhantoUtils.VR;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Controls the "goo" in the game
/// </summary>
public class GooController : MonoBehaviour
{
    [SerializeField] private ParticleSystem impactDustPS;
    private ParticleSystem _particleSystem;

    private GameObject _prefab;

    public Action Stopped;
    [SerializeField] private float extinguishAccelerationSpeed = 2.5f;

    private void Awake()
    {
        _particleSystem = GetComponent<ParticleSystem>();
    }

    private void OnEnable()
    {
#if DEBUG
        if (_particleSystem.main.stopAction != ParticleSystemStopAction.Callback)
            Debug.LogWarning("Particle systems needs to have callback stop action for controller to work.", this);
#endif
        SpeedUpParticles(1);
        _particleSystem.Play(true);
    }

    private void OnParticleSystemStopped()
    {
        if (_prefab != null) PoolManagerSingleton.Instance.Discard(_prefab);
        Stopped?.Invoke();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_particleSystem == null) _particleSystem = GetComponent<ParticleSystem>();
    }
#endif

    public void Extinguish(GameObject prefab)
    {
        _prefab = prefab;
        SpeedUpParticles(extinguishAccelerationSpeed);
        _particleSystem.Stop(true);
    }

    public void ImpactBlink()
    {
        if (impactDustPS)
        {
            impactDustPS.Stop();
            impactDustPS.Play();
        }
    }

    public void SpeedUpParticles( float acceleration )
    {
        SpeedUp(_particleSystem, acceleration);
        var particles = _particleSystem.GetComponentsInChildren<ParticleSystem>();
        foreach (var child in particles)
        {
            SpeedUp(child, acceleration);
        }
    }

    private void SpeedUp(ParticleSystem particles, float acceleration)
    {
#if DEBUG
        Debug.Log($"{Application.productName}: Speeding up {particles.name} by a factor of {acceleration}");
#endif
        var particlesMain = particles.main;
        particlesMain.simulationSpeed = acceleration;
    }
}
