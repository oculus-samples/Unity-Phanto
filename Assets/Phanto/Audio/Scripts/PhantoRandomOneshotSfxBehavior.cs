// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using UnityEngine;

namespace Phanto.Audio.Scripts
{
    /// <summary>
    ///     Play a random oneshot sfx on Start.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PhantoRandomOneshotSfxBehavior : MonoBehaviour
    {
        public bool playOnAwake;
        public AudioClip[] clips;
        [Range(0.01f, 2)] public float pitchMin = 1;
        [Range(0.01f, 2)] public float pitchMax = 1;
        [Range(0, 100)] public int chanceToPlay = 100;

        [Range(0, 2)] public float startDelayMin;
        [Range(0, 2)] public float startDelayMax;
        public AudioSource src;

        private void Awake()
        {
            src = GetComponent<AudioSource>();
        }

        private void Start()
        {
            if (clips.Length <= 0) Debug.LogWarning("No clips proviced for RandomOneshotSfx");
            if (playOnAwake) PlaySfx();
        }


#if UNITY_EDITOR
        private void OnValidate()
        {
            if (src == null) src = GetComponent<AudioSource>();
        }
#endif

        public void PlaySfx()
        {
            if (clips.Length <= 0) return;
            src.clip = clips[Random.Range(0, clips.Length - 1)];
            src.pitch = Random.Range(pitchMin, pitchMax);
            if (Random.Range(0, 100) <= chanceToPlay)
                StartCoroutine(WaitAndPlay(Random.Range(startDelayMin, startDelayMax)));
        }

        public void PlaySfxAtPosition(Vector3 pos)
        {
            transform.position = pos;
            PlaySfx();
        }

        private IEnumerator WaitAndPlay(float waitTime)
        {
            yield return new WaitForSeconds(waitTime);
            src.Play();
        }
    }
}
