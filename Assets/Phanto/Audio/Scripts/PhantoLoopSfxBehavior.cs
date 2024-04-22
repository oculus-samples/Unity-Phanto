// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Phanto.Audio.Scripts
{
    /// <summary>
    /// A MonoBehavior attached to GameObjects which will play a sound
    /// when receiving a 'PhantoLoopSfxBehavior' event.
    /// </summary>
    public class PhantoLoopSfxBehavior : MonoBehaviour
    {
        public bool isOn;

        [SerializeField] protected AudioClip[] loops;
        [SerializeField] protected AudioClip[] starts;
        [SerializeField] protected AudioClip[] stops;
        [SerializeField] internal AudioSource startSrc;
        [SerializeField] internal AudioSource loopSrc;
        [SerializeField] internal AudioSource stopSrc;

        [Range(0, 1)] [SerializeField] protected float loopVolume = 1;
        [Range(0, 5)] [SerializeField] protected float loopFadeInTime = 1;
        [Range(0, 5)] [SerializeField] protected float loopFadeOutTime = 0.1f;
        [Range(0, 1)] [SerializeField] protected float startVolume = 1;
        [Range(0, 1)] [SerializeField] protected float stopVolume = 1;
        [SerializeField] protected bool randomStartPosition = true;

        [SerializeField] protected float bufferStartTime;

        public UnityEvent loopBeginEvent = new UnityEvent();

        /// <summary>
        /// Play a sound when receiving a 'PhantoLoopSfx
        /// </summary>
        public void StartSfx(bool waitForIntroToEnd = false)
        {
            StopAllCoroutines();
            isOn = true;
            var startBuffer = AudioSettings.dspTime + bufferStartTime;
            if (startSrc && gameObject.activeInHierarchy && starts.Length > 0)
            {
                startSrc.clip = starts[UnityEngine.Random.Range(0, starts.Length)];
                startSrc.volume = startVolume;
                if (bufferStartTime != 0)
                    startSrc.PlayScheduled(startBuffer);
                else
                    startSrc.Play();
            }

            if (loopSrc && gameObject.activeInHierarchy && loops.Length > 0)
            {
                loopSrc.Stop();
                loopSrc.clip = loops[UnityEngine.Random.Range(0, loops.Length)];
                double waitTime = 0;
                if (waitForIntroToEnd) waitTime = (double)startSrc.clip.samples / startSrc.clip.frequency;

                if (bufferStartTime != 0)
                {
                    loopSrc.PlayScheduled(startBuffer + waitTime);
                    StartCoroutine(WaitAndFireEvent(bufferStartTime+waitTime));
                }
                else
                {
                    loopSrc.PlayScheduled(AudioSettings.dspTime + waitTime);
                    StartCoroutine(WaitAndFireEvent(waitTime));
                }
                if (randomStartPosition)
                    loopSrc.time = UnityEngine.Random.Range(0, loopSrc.clip.length);
                StartCoroutine(Fade(0, loopVolume, loopFadeInTime, loopSrc));
            }
        }

        private IEnumerator FireLoopEvent(float waitTime)
        {
            yield return new WaitForSeconds(waitTime);
            loopBeginEvent.Invoke();
        }

        private IEnumerator WaitAndFireEvent(double waitTime)
        {
            double startTime = AudioSettings.dspTime;
            while (AudioSettings.dspTime - startTime < waitTime)
            {
                yield return null;
            }
            loopBeginEvent.Invoke();
        }
        /// <summary>
        /// Stop playing the sound when receiving a 'PhantoLoopSfx
        /// </summary>
        public void StopSfx()
        {
            isOn = false;
            StopAllCoroutines();
            if (starts.Length > 0 && startSrc != null)
                if (startSrc.isPlaying) StartCoroutine(Fade(startSrc.volume, 0, loopFadeOutTime, startSrc));
            if (loops.Length > 0 &&  loopSrc != null)
                if (loopSrc.isPlaying) StartCoroutine(Fade(loopSrc.volume, 0, loopFadeOutTime, loopSrc));
            if (stops.Length > 0 && stopSrc != null)
            {
                if (stopSrc && gameObject.activeInHierarchy && stops.Length > 0)
                {
                    stopSrc.clip = stops[UnityEngine.Random.Range(0, stops.Length)];
                    stopSrc.volume = stopVolume;
                    stopSrc.Play();
                }
            }
        }

        public void ForceStop()
        {
            if(startSrc != null)
                startSrc.Stop();
            if (loopSrc != null)
                loopSrc?.Stop();
        }

        /// <summary>
        /// Fade the sound in/out
        /// </summary>
        private IEnumerator Fade(float initialValue, float targetValue, float fadeInDuration, AudioSource audioSource)
        {
            var elapsedTime = 0f;
            while (elapsedTime < fadeInDuration)
            {
                audioSource.volume = Mathf.Lerp(initialValue, targetValue, elapsedTime / fadeInDuration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            audioSource.volume = targetValue;
            if (audioSource.loop && targetValue == 0) audioSource.Stop();
        }
    }
}
