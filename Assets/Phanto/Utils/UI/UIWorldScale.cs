// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;

namespace PhantoUtils
{
    /// <summary>
    ///     Animates the ellipsis of the given text. Uses maxVisibleCharacters to control the ellipsis visibility instead of
    ///     appending and removing characters.
    ///     Usage: Attach to your text or assign the target text in the inspector. Include the ellipsis in your text.
    /// </summary>
    public class UIWorldScale : MonoBehaviour
    {
        [SerializeField] private RectTransform targetTransform;

        [SerializeField] private ScaleMode scaleMode = ScaleMode.Width;

        [SerializeField] private float worldWidth = 1.0f;
        [SerializeField] private float worldHeight = 1.0f;

        private void OnEnable()
        {
            ResizeTransform();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (targetTransform == null) targetTransform = GetComponentInChildren<RectTransform>();

            if (enabled) ResizeTransform();
        }
#endif

        private void ResizeTransform()
        {
            var scale = targetTransform.localScale;
            var rect = targetTransform.rect;
            switch (scaleMode)
            {
                case ScaleMode.Width:
                    scale.x = worldWidth / rect.width;
                    scale.y = scale.x;
                    worldHeight = worldWidth * (rect.height / rect.width);
                    break;
                case ScaleMode.Height:
                    scale.y = worldHeight / rect.height;
                    scale.x = scale.y;
                    worldWidth = worldHeight * (rect.width / rect.height);
                    break;
                case ScaleMode.Separate:
                    scale.x = worldWidth / rect.width;
                    scale.y = worldHeight / rect.height;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            targetTransform.localScale = scale;
        }

        private enum ScaleMode
        {
            Width,
            Height,
            Separate
        }
    }
}
