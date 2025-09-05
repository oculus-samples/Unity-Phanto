// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using Phanto.Haptic.Scripts;
using UnityEngine;

[MetaCodeSample("Phanto")]
public class HapticsPhantoReaction : MonoBehaviour
{
    [SerializeField] private PhantoRandomOneShotHapticSfxBehavior hapticSfxBehavior;

    private void OnTriggerEnter(Collider other)
    {
        if (!HapticsPhantoTouch.TryGetPhantoTouch(other, out var phantoTouch))
        {
            return;
        }

        hapticSfxBehavior.PlayHapticOnController(phantoTouch.Controller);
    }
}
