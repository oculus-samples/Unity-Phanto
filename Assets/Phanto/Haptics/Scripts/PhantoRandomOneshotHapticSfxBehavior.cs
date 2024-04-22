// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Haptics;
using Phanto.Audio.Scripts;
using PhantoUtils;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Phanto.Haptic.Scripts
{
    /// <summary>
    ///     Play a random oneshot sfx on Start.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PhantoRandomOneshotHapticSfxBehavior : PhantoRandomOneShotSfxBehavior
    {
        [Serializable]
        public class AudioHapticPair
        {
            public AudioClip audio;
            public HapticClip haptic;
        }

        [SerializeField] private AudioHapticPair[] audioHapticClips;

        [SerializeField] private Controller hapticController = Controller.Both;

        private Dictionary<HapticClip, HapticClipPlayer> _clipPlayers = new Dictionary<HapticClip, HapticClipPlayer>();

        private void OnDestroy()
        {
            foreach (var player in _clipPlayers.Values)
            {
                player?.Dispose();
            }

            _clipPlayers.Clear();
        }

        public override void PlaySfx()
        {
            PlayHapticOnController(hapticController);
        }

        public void PlayHapticOnController(Controller controller)
        {
            if (audioHapticClips.Length == 0)
            {
                base.PlaySfx();
                return;
            }

            var pair = audioHapticClips.RandomElement();

            if (Random.Range(0, 100) <= chanceToPlay)
            {
                StartCoroutine(WaitAndPlay(pair, Random.Range(startDelayMin, startDelayMax), controller));
            }
        }

        private IEnumerator WaitAndPlay(AudioHapticPair pair, float waitTime, Controller controller)
        {
            yield return new WaitForSeconds(waitTime);

            src.clip = pair.audio;
            src.pitch = Random.Range(pitchMin, pitchMax);

            src.Play();

            PlayHapticClip(pair.haptic, controller);
        }

        private void PlayHapticClip(HapticClip hapticClip, Controller controller)
        {
            if (!_clipPlayers.TryGetValue(hapticClip, out var player))
            {
                player = new HapticClipPlayer(hapticClip);
                _clipPlayers.Add(hapticClip, player);
            }

            player?.Play(controller);
        }
    }
}
