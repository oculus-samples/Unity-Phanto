// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Phanto.Audio.Scripts;
using UnityEngine;

/// <summary>
/// Class containing logic for the "Goo"
/// </summary>
public class PhantoGoo : MonoBehaviour
{
    private static readonly HashSet<PhantoGoo> GooCollection = new();

    [Tooltip("The GooController this Goo belongs to.")]
    [SerializeField] private GooController gooController;

    [Tooltip("The collider this Goo uses to detect collisions.")]
    [SerializeField] private new Collider collider;

    [SerializeField] private LayerMask ectoBlasterLayer;

    public int splashHitsToExtinguish = 20;
    public PhantoLoopSfxBehavior gooLoopSfx;
    public static IReadOnlyCollection<PhantoGoo> ActiveGoos => GooCollection;
    public Vector3 Position => transform.position;

    private int _splashHits;

    private void Start()
    {
        PhantoGooSfxManager.Instance.activeGoos.Add(this);
    }

    private void OnEnable()
    {
        _splashHits = 0;
        collider.enabled = true;
        PhantoGooSfxManager.Instance.PlayGooStartSound(transform.position);
        GooCollection.Add(this);
    }

    private void OnDisable()
    {
        if (PhantoGooSfxManager.Instance != null) PhantoGooSfxManager.Instance.activeGoos.Remove(this);
        GooCollection.Remove(this);
    }

    private void OnDestroy()
    {
        if (PhantoGooSfxManager.Instance != null) PhantoGooSfxManager.Instance.activeGoos.Remove(this);
    }

    /// <summary>
    /// Triggered when the collider hits the Goo.
    /// </summary>
    private void OnParticleCollision(GameObject other)
    {
#if VERBOSE_DEBUG
        Debug.Log("Goo Trigger hit with splash");
#endif
        gooController.ImpactBlink();

        _splashHits++;
        if (_splashHits >= splashHitsToExtinguish)
        {
            Extinguish();
        }

        var isEctoBlaster = ectoBlasterLayer == (ectoBlasterLayer | (1 << other.layer));

        if (!isEctoBlaster)
        {
            PolterblastTrigger.SplashHitNotification();
        }
    }

    public void Extinguish()
    {
        PhantoGooSfxManager.Instance.PlayGooStopSound(transform.position);
        gooController.Extinguish(gameObject);
        collider.enabled = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gooController == null) gooController = GetComponent<GooController>();

        if (collider == null) collider = GetComponent<Collider>();
    }
#endif
}
