// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace PhantoUtils.VR
{
    [DefaultExecutionOrder(-10000)]
    public class DebugLogScrollingTextView : MonoBehaviour
    {
        [SerializeField] private int maxBatches = 128;
        [SerializeField] private int batchCharacterSize = 512;

        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private TextMeshProUGUI logLinePrefab;

        [SerializeField] private OVRInput.Controller scrollInputSource = OVRInput.Controller.None;

        private bool _logTextDirty;

        private StringBuilder _messageBuffer;
        private bool _scrollToLatestLine = true;

        private readonly LinkedList<TextMeshProUGUI> _textBatches = new();
        private int _updateTickDelay;

        private ScrollviewControls _scrollviewControls;

        private void Awake()
        {
            Assert.IsNotNull(logLinePrefab);
            Assert.IsNotNull(scrollRect);

            _updateTickDelay = Mathf.Max(Application.targetFrameRate, 60) / 8;
            _messageBuffer = new StringBuilder(batchCharacterSize * 4);

            Application.logMessageReceived += Log;

            _scrollviewControls = GetComponentInChildren<ScrollviewControls>();
            Assert.IsNotNull(_scrollviewControls);
        }

        private void Update()
        {
            if (_logTextDirty && Time.frameCount % _updateTickDelay == 0) // Only update text every n ticks
            {
                UpdateTextBatches();
                _logTextDirty = false;
            }

            var scrollToLatest = _scrollToLatestLine;

            if (scrollInputSource != OVRInput.Controller.None)
            {
                var thumbstickTouch = OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, scrollInputSource);

                // If the thumb stick is being touched stop scrolling to the most recent line.
                scrollToLatest = scrollToLatest && !thumbstickTouch;

                if (thumbstickTouch)
                {
                    _scrollviewControls.SetScroll(OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, scrollInputSource));
                }
            }

            if (scrollToLatest) scrollRect.verticalNormalizedPosition = 0.0f;
        }

        private void OnEnable()
        {
            scrollRect.verticalNormalizedPosition = 0.0f;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= Log;
        }

        private void UpdateTextBatches()
        {
            TextMeshProUGUI batch;
            if (_textBatches.Count == 0 || _textBatches.Last.Value.text.Length > 512)
            {
                if (_textBatches.Count >= maxBatches)
                {
                    // Recycle the oldest batch
                    batch = _textBatches.First.Value;
                    _textBatches.RemoveFirst();
                    _textBatches.AddLast(batch);
                    batch.transform.SetAsLastSibling();
                }
                else
                {
                    // Start a new batch
                    batch = Instantiate(logLinePrefab, scrollRect.content);
                    _textBatches.AddLast(batch);
                }
            }
            else
            {
                // Modify the previous batch
                batch = _textBatches.Last.Value;
            }

            batch.text = _messageBuffer.ToString();
            batch.ForceMeshUpdate();
        }

        private void Log(string message, string stacktrace, LogType type)
        {
            _logTextDirty = true;
            _messageBuffer.Append(GetTime());
            _messageBuffer.Append(" ");
            _messageBuffer.AppendLine(message);
            if (type == LogType.Exception) _messageBuffer.AppendLine(stacktrace);

            // If the buffer is past the target batch size, update batches immediately and flush
            if (_messageBuffer.Length > batchCharacterSize)
            {
                UpdateTextBatches();
                _messageBuffer.Clear();
            }
        }

        public void SetScrollToLatestLine(bool shouldScroll)
        {
            _scrollToLatestLine = shouldScroll;
        }

        private static string GetTime()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff");
        }

        private void OnValidate()
        {
            switch (scrollInputSource)
            {
                // only allow this field to have the following values:
                case OVRInput.Controller.LTouch:
                case OVRInput.Controller.RTouch:
                case OVRInput.Controller.Touch:
                case OVRInput.Controller.None:
                    break;
                default:
                    scrollInputSource = OVRInput.Controller.None;
                    break;
            }
        }
    }
}
