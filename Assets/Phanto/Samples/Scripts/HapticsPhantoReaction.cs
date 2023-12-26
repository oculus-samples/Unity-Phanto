// Copyright (c) Meta Platforms, Inc. and affiliates.

using Phanto.Haptic.Scripts;
using UnityEngine;

public class HapticsPhantoReaction : MonoBehaviour
{
    [SerializeField] private PhantoRandomOneshotHapticSfxBehavior hapticSfxBehavior;

    private void OnTriggerEnter(Collider other)
    {
        if (!HapticsPhantoTouch.TryGetPhantoTouch(other, out var phantoTouch))
        {
            return;
        }

        hapticSfxBehavior.PlayHapticOnController(phantoTouch.Controller);
    }
}
