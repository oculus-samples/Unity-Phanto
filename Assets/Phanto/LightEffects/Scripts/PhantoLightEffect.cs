// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Runtime.CompilerServices;
using UnityEngine;

namespace Phantom.LightEffects.Scripts
{
    /// <summary>
    ///     LightEffect is a simple script that applies a crossfade
    ///     effect to a mesh renderer on tracking. This is used to apply
    ///     lighting effects on Phanto.
    /// </summary>
    public class PhantoLightEffect : MonoBehaviour
    {
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int TextureRotationId = Shader.PropertyToID("_TextureRotation");
        private static readonly int SourcePosId = Shader.PropertyToID("_SourcePos");

        [SerializeField] private float rotateSpeed;
        private bool _active;

        private float _prevBlend;

        private Transform _trackingTransform;

        private void Start()
        {
            var t = Timebase(rotateSpeed);
            _prevBlend = CrossFadePerlin(t);
        }

        private void Update()
        {
            var t = Timebase(rotateSpeed);

            var blend = CrossFadePerlin(t);

            Shader.SetGlobalFloat(BlendId, blend);

            // change second texture on the downstroke of the blend function
            if (blend > _prevBlend)
            {
                var rot = Quaternion.Euler(Random.Range(-180.0f, 180.0f), 0, Random.Range(-180.0f, 180.0f));
                var m = Matrix4x4.TRS(Vector3.zero, rot, Vector3.one);
                Shader.SetGlobalMatrix(TextureRotationId, m);
            }

            _prevBlend = blend;

            Vector4 pos = default;
            if (_active)
            {
                pos = _trackingTransform.position;
                pos.w = _trackingTransform.gameObject.activeInHierarchy ? 1.0f : 0.0f;
            }

            Shader.SetGlobalVector(SourcePosId, pos);
        }

        public void Register(Transform tracking)
        {
            _trackingTransform = tracking;
            _active = _trackingTransform != null;
        }

        public void Unregister(Transform tracking)
        {
            if (_trackingTransform == tracking) Register(null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CrossFadeSin(float t)
        {
            return 0.5f * (Mathf.Sin(t) + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CrossFadePerlin(float t)
        {
            return Mathf.PerlinNoise(0.5f, t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Timebase(float s)
        {
            return Time.time * s;
        }
    }
}
