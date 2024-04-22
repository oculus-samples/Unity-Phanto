// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Linq;
using Phanto.Enemies.DebugScripts;
using UnityEngine;
using Utilities.XR;

namespace Phantom
{
    public class PhantomFleeTarget : PhantomTarget
    {
        private static readonly SpatialHash<PhantomFleeTarget> FleeTargetHash =
         new SpatialHash<PhantomFleeTarget>(NavMeshConstants.OneFoot);

        [SerializeField] private bool decay;
        [SerializeField] private float decayDuration = 2.0f;
        [SerializeField] private SphereCollider _sphereCollider;
        private Coroutine _decayCoroutine;
        private Vector3 _initialScale;

        private Transform _transform;

        public override bool Flee => true;

        public Vector3 FleeVector => _fleeVector;
        private Vector3 _fleeVector = Vector3.zero;

        public override Vector3 Position
        {
            get => transform.position;
            set => transform.position = value;
        }

        public override bool Valid => isActiveAndEnabled;

        private void Awake()
        {
            _transform = transform;
            _initialScale = _transform.localScale;
        }

        protected override void OnEnable()
        {
            Register(this, _sphereCollider);

            DebugDrawManager.DebugDrawEvent += DebugDraw;
        }

        protected override void OnDisable()
        {
            if (_decayCoroutine != null)
            {
                StopCoroutine(_decayCoroutine);
                _decayCoroutine = null;
            }

            base.OnDisable();

            Unregister(this, _sphereCollider);
            DebugDrawManager.DebugDrawEvent -= DebugDraw;

            FleeTargetHash.Remove(this);
        }

        public bool IsInBounds(Vector3 point)
        {
            var distance = Vector3.Distance(_transform.position, point);
            return distance <= Mathf.Abs(_transform.lossyScale.x * _sphereCollider.radius);
        }

        private IEnumerator Decay(float duration)
        {
            _transform.localScale = _initialScale;

            var elapsed = 0.0f;

            // Target shrinks until it returns to the pool.
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / duration;
                _transform.localScale = Vector3.Lerp(_initialScale, Vector3.zero, t);
                yield return null;
            }

            _decayCoroutine = null;
            Hide();
        }

        public override void Initialize(OVRSemanticClassification classification, OVRSceneRoom _)
        {

        }

        public void SetPositionAndDirection(Vector3 position, Vector3 normal)
        {
            _fleeVector = normal;
            Position = position;
            FleeTargetHash.Add(position, this);
        }

        public void UpdateDirection(Vector3 normal)
        {
            _fleeVector = (_fleeVector + normal) * 0.5f;
        }

        public override void TakeDamage(float f)
        {
        }

        public override Vector3 GetAttackPoint()
        {
            return transform.position;
        }

        public override Vector3 GetDestination(Vector3 point, float min = 0.0f, float max = 0.0f)
        {
            return Position;
        }

        public override void Show(bool visible = true)
        {
            gameObject.SetActive(visible);

            if (visible && decay) _decayCoroutine = StartCoroutine(Decay(decayDuration));
        }

        private void DebugDraw()
        {
            try
            {
                XRGizmos.DrawWireSphere(_transform.position, _transform.lossyScale.y, Color.red);
            }
            catch (Exception e)
            {
                Debug.LogWarning("PhantomFleeTarget: caught it.");
                Debug.LogException(e);
            }
        }

        public static bool TryGetFleeTarget(Vector3 position, out PhantomFleeTarget result)
        {
            // check spatial hash to see if there's an active ouch near position.
            if (FleeTargetHash.TryGetCell(position, out var targets) && targets.Count > 0)
            {
                result = targets.First();
                return true;
            }

            result = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, _fleeVector);

            if (TryGetComponent(out SphereCollider collider))
            {
                var matrix = transform.localToWorldMatrix;

                Gizmos.matrix = matrix;
                Gizmos.DrawWireSphere(collider.center, collider.radius);
            }
        }

        private void OnValidate()
        {
            if (_sphereCollider == null) _sphereCollider = GetComponent<SphereCollider>();
        }
#endif
    }
}
