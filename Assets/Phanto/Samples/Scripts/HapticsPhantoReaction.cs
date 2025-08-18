// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using Phanto.Haptic.Scripts;
using UnityEngine;

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
