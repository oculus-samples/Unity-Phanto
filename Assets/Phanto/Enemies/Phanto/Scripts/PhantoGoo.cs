// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using Phanto.Audio.Scripts;
using UnityEngine;

/// <summary>
/// Class containing logic for the "Goo"
/// </summary>
public class PhantoGoo : MonoBehaviour
{
    private static readonly HashSet<PhantoGoo> GooCollection = new();
    public static IReadOnlyCollection<PhantoGoo> ActiveGoos => GooCollection;

    [Tooltip("The GooController this Goo belongs to.")] [SerializeField]
    private GooController gooController;

    [Tooltip("The collider this Goo uses to detect collisions.")] [SerializeField]
    private new Collider collider;

    [SerializeField] private LayerMask ectoBlasterLayer;

    [SerializeField] private int splashHitsToExtinguish = 20;
    [SerializeField] private PhantoLoopSfxBehavior gooLoopSfx;

    private int _splashHits;
    private Transform _transform;

    public Vector3 Position => _transform.position;

    private void Awake()
    {
        _transform = transform;
    }

    private void OnEnable()
    {
        _splashHits = 0;
        collider.enabled = true;
        PhantoGooSfxManager.Instance.RegisterGoo(this);
        PhantoGooSfxManager.Instance.PlayGooStartSound(Position);
        GooCollection.Add(this);
    }

    private void OnDisable()
    {
        if (PhantoGooSfxManager.Instance != null) PhantoGooSfxManager.Instance.UnregisterGoo(this);
        GooCollection.Remove(this);
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

    public void Extinguish(bool playSound = true)
    {
        if (playSound)
        {
            PhantoGooSfxManager.Instance.PlayGooStopSound(Position);
        }

        gooController.Extinguish(gameObject);
        collider.enabled = false;
    }

    public void StartSfx()
    {
        if (!gooLoopSfx.isOn)
        {
            gooLoopSfx.StartSfx();
        }
    }

    public void StopSfx()
    {
        if (gooLoopSfx.isOn)
        {
            gooLoopSfx.StopSfx();
        }
    }

    public static IEnumerator ExtinguishAllGoo()
    {
        var gooList = new List<PhantoGoo>(GooCollection.Count);

        while (GooCollection.Count > 0)
        {
            gooList.AddRange(GooCollection);

            for (var i = 0; i < gooList.Count; i++)
            {
                gooList[i].Extinguish(false);
                yield return null;
            }

            gooList.Clear();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gooController == null) gooController = GetComponent<GooController>();

        if (collider == null) collider = GetComponent<Collider>();
    }
#endif
}
