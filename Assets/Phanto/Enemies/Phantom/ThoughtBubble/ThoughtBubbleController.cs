// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using PhantoUtils.VR;
using UnityEngine;
using UnityEngine.Assertions;

namespace Phantom
{
    public class ThoughtBubbleController : MonoBehaviour
    {
        private const float GROW_DURATION = 0.5f;

        [SerializeField] private SpriteRenderer thoughtRenderer;
        [SerializeField] private SpriteRenderer bubbleRenderer;

        [SerializeField] private AnimationCurve scaleUp;

        [SerializeField] private ThoughtIconCollection thoughtIcons;

        private Transform _transform;

        private Coroutine bubbleCoroutine = null;

        private Transform _head;

        private void Awake()
        {
            _transform = transform;

            _transform.localScale = Vector3.zero;
            Show(false);
        }

        private void OnDisable()
        {
            if (bubbleCoroutine != null)
            {
                StopCoroutine(bubbleCoroutine);
                bubbleCoroutine = null;
                Show(false);
            }
        }

        private void Start()
        {
            _head = CameraRig.Instance.CenterEyeAnchor;

            Assert.IsNotNull(_head);
        }

        private void LateUpdate()
        {
            if (bubbleCoroutine == null)
            {
                return;
            }

            var headVector = Vector3.ProjectOnPlane(_head.position - _transform.position, Vector3.up).normalized;

            _transform.rotation = Quaternion.LookRotation(headVector, Vector3.up);
        }

        public void ShowThought(Thought thought, float duration = 1.0f)
        {
            if (bubbleCoroutine != null)
            {
                thoughtRenderer.enabled = false;
                StopCoroutine(bubbleCoroutine);
            }

            bubbleCoroutine = StartCoroutine(AnimateThoughtBubble(thought, duration));
        }

        private IEnumerator AnimateThoughtBubble(Thought thought, float duration)
        {
            // swap icon
            if (!thoughtIcons.TryGetIcon(thought, out Sprite icon))
            {
                Debug.LogWarning($"No icon for thought: {thought}");
                yield break;
            }

            thoughtRenderer.sprite = icon;

            // enable bubble & icon renderers
            Show();

            // scale up
            yield return StartCoroutine(ScaleBubble(GROW_DURATION));

            // wait duration
            yield return new WaitForSeconds(duration);

            // scale down
            yield return StartCoroutine(ScaleBubble(GROW_DURATION, 1.0f, 0.0f));

            // disable bubble & icon.
            Show(false);

            bubbleCoroutine = null;
        }

        private IEnumerator ScaleBubble(float duration, float start = 0.0f, float end = 1.0f)
        {
            var startScale = new Vector3(start, start, start);
            var endScale = new Vector3(end, end, end);

            _transform.localScale = startScale;

            var elapsed = 0.0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                var value = scaleUp.Evaluate(elapsed / duration);

                var scale = MathUtils.Remap(0, 1, start, end, value);

                _transform.localScale = new Vector3(scale, scale, scale);

                yield return null;
            }

            _transform.localScale = endScale;
        }

        private void Show(bool visible = true)
        {
            thoughtRenderer.enabled = visible;
            bubbleRenderer.enabled = visible;
        }
    }
}
