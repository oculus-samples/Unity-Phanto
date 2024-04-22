// Copyright (c) Meta Platforms, Inc. and affiliates.

using PhantoUtils.VR;
using TMPro;
using UnityEngine;

namespace Phanto
{
    /// <summary>
    ///     Scorebar for enemies
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class PhantoScorebar : MonoBehaviour
    {
        private Transform _cameraTransform;
        private Enemy _enemy;
        private TextMeshPro _textMesh;

        private void Start()
        {
            _enemy = GetComponentInParent<Enemy>();
            _textMesh = GetComponent<TextMeshPro>();
            _cameraTransform = CameraRig.Instance.CenterEyeAnchor;
        }

        private void Update()
        {
            // Update score
            _textMesh.text = new string('-', Mathf.Max(0,(int)(_enemy.Health / 10)));
            var source = transform;
            var position = source.position;
            var dirToTarget = (_cameraTransform.position - position).normalized;
            transform.LookAt(position - dirToTarget, Vector3.up);
        }
    }
}
