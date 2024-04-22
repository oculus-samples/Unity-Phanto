// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using Oculus.Haptics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Phanto.Audio.Scripts
{
    /// <summary>
    ///     Play a random oneshot sfx on Start.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PhantoRandomOneShotSfxBehavior : MonoBehaviour
    {
        [SerializeField] private bool playOnAwake;
        [SerializeField] protected AudioClip[] clips;

        [SerializeField][Range(0.01f, 3)] public float pitchMin = 1;
        [SerializeField][Range(0.01f, 3)] public float pitchMax = 1;
        [SerializeField][Range(0, 100)] protected int chanceToPlay = 100;

        [SerializeField][Range(0, 2)] protected float startDelayMin;
        [SerializeField][Range(0, 2)] protected float startDelayMax;
        [SerializeField] protected AudioSource src;

        public float ClipLength => src.clip.length;

        private void Awake()
        {
            src = GetComponent<AudioSource>();
        }

        protected virtual void Start()
        {
            if (clips.Length <= 0) Debug.LogWarning("No clips provided for RandomOneShotSfx", this);
            if (playOnAwake) PlaySfx();
            Random.InitState((int)System.DateTime.Now.Ticks);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (src == null) src = GetComponent<AudioSource>();
        }
#endif

        public virtual void PlaySfx()
        {
            if (clips.Length <= 0) return;

            src.clip = clips[Random.Range(0, clips.Length)];
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
