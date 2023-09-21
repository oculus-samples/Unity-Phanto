// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using UnityEngine;

namespace Phantom.Environment.Scripts
{
    /// <summary>
    ///     This component previews a mesh renderer on a given GameObject.
    /// </summary>
    public class MeshPreview : MonoBehaviour
    {
        [SerializeField] private string propertyName = "_Color";

        [SerializeField] private Color _baseColor;
        [SerializeField] private float _frequency = 1f;
        [SerializeField] private float _amplitude = 0.5f;
        private Material mat;

        private int propertyId;

        private void Awake()
        {
            propertyId = Shader.PropertyToID(propertyName);
        }

        private IEnumerator Start()
        {
            mat = GetComponent<MeshRenderer>().material;
            yield return null;
        }

        private void Update()
        {
            var t = Time.time * _frequency;
            var r = Mathf.Sin(t) * _amplitude + _baseColor.r;
            var g = Mathf.Sin(t + Mathf.PI / 3f) * _amplitude + _baseColor.g;
            var b = Mathf.Sin(t + Mathf.PI * 2f / 3f) * _amplitude + _baseColor.b;

            var color = new Color(r, g, b, 0.1f + Mathf.Sin(t) / 2);
            mat.SetColor(propertyId, color);
        }
    }
}
