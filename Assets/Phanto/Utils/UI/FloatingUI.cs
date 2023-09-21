// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace PhantoUtils.VR
{
    public class FloatingUI : MonoBehaviour
    {
        [SerializeField] private Vector3 targetPosition = new(0.0f, 0.1f, 1.0f);

        public float softLeashSpeed = 2.0f;

        [SerializeField]
        [Tooltip(
            "The UI will gradually return to the center of the player's gaze when the player looks outside this area. Width and height are relative to the UI size, depth is in world units.")]
        private Vector3 softLeashSize = new(0.8f, 0.8f, 0.1f);

        [SerializeField]
        [Tooltip(
            "The UI will be restricted within the leash area. Width and height are relative to the UI size, depth is in world units.")]
        private Vector3 hardLeashSize = new(2.0f, 2.0f, 0.15f);

        [SerializeField]
        [Tooltip(
            "The UI will gradually return to the center of the player's gaze when the player looks outside this area. Width and height are relative to the UI size, depth is in world units.")]
        private Vector3 recenterTriggerSize = new(2.0f, 2.0f, 0.15f);

        public float recenterSpeed = 0.5f;
        public float recenterArriveRadius = 0.1f;
        public bool recenterX = true;
        public bool recenterY;
        public bool recenterZ;

        public bool constrainY = true;
        [SerializeField] private Vector2 constrainYMinMax = new(-0.1f, 0.25f);

        public bool lockPitch = true;
        private bool _isRecentering;
        private Vector3 _recenterWorldPosition;

        private RectTransform _rectTransform;

        public Vector3 TargetPosition
        {
            get => targetPosition;
            set
            {
                targetPosition = value;
                targetPosition.z = Mathf.Max(0.01f, targetPosition.z);
            }
        }

        public Vector3 SoftLeashSize
        {
            get => softLeashSize;
            set => softLeashSize = Vector3.Max(value, Vector3.zero);
        }

        public Vector3 HardLeashSize
        {
            get => hardLeashSize;
            set
            {
                hardLeashSize = Vector3.Max(value, softLeashSize);
                hardLeashSize.z = Mathf.Min(hardLeashSize.z, TargetPosition.z - 0.01f);
            }
        }

        public Vector3 RecenterTriggerSize
        {
            get => recenterTriggerSize;
            set => recenterTriggerSize = Vector3.Min(Vector3.Max(value, softLeashSize), hardLeashSize);
        }

        public Vector2 ConstrainYMinMax
        {
            get => constrainYMinMax;
            set => constrainYMinMax = new Vector2(value.x, Mathf.Max(value.y, value.x));
        }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void Update()
        {
            var cameraTransform = CameraRig.Instance.CenterEyeAnchor;
            var cameraPosition = cameraTransform.position;

            var uiWorldPosition = transform.position;
            var uiPosition = cameraTransform.InverseTransformPoint(uiWorldPosition);


            var hardLeashExtents = GetScaledExtents(HardLeashSize);
            var softLeashExtents = GetScaledExtents(softLeashSize);
            var recenterExtents = GetScaledExtents(RecenterTriggerSize);
            var toTarget = TargetPosition - uiPosition;

            // Hard leash - Restrict position within leash area
            uiPosition = Vector3.Min(Vector3.Max(uiPosition, TargetPosition - hardLeashExtents),
                TargetPosition + hardLeashExtents);

            // Recenter - Float back to the center of the view
            if ((recenterX && Mathf.Abs(toTarget.x) > recenterExtents.x)
                || (recenterY && Mathf.Abs(toTarget.y) > recenterExtents.y)
                || (recenterZ && Mathf.Abs(toTarget.z) > recenterExtents.z))
            {
                _recenterWorldPosition = cameraTransform.TransformPoint(TargetPosition);
                _isRecentering = true;
            }

            if (_isRecentering)
            {
                // Leash the recenter target by the arrive radius
                var recenterTarget = cameraTransform.InverseTransformPoint(_recenterWorldPosition);
                recenterTarget =
                    Vector3.Min(Vector3.Max(recenterTarget, TargetPosition - Vector3.one * recenterArriveRadius),
                        TargetPosition + Vector3.one * recenterArriveRadius);
                var toRecenterTarget = recenterTarget - uiPosition;

                // Recenter
                if ((!recenterX || Mathf.Abs(toRecenterTarget.x) <= recenterArriveRadius)
                    && (!recenterY || Mathf.Abs(toRecenterTarget.y) <= recenterArriveRadius)
                    && (!recenterZ || Mathf.Abs(toRecenterTarget.z) <= recenterArriveRadius))
                {
                    _isRecentering = false;
                }
                else
                {
                    recenterTarget = Vector3.Slerp(uiPosition, recenterTarget, recenterSpeed * Time.deltaTime);
                    recenterTarget = GetConstrainedRecenterTarget(recenterTarget, uiPosition);
                    uiPosition = recenterTarget;
                }
            }

            // Soft leash - Float back into view gradually when outside leash area
            if (IsPointOutsideExtents(toTarget, softLeashExtents, out var softLeashAxes))
            {
                var leashTarget = toTarget - Vector3.Scale(Sign(toTarget), softLeashExtents);
                leashTarget = Vector3.Scale(leashTarget, softLeashAxes);
                leashTarget = Vector3.Slerp(Vector3.zero, leashTarget, softLeashSpeed * Time.deltaTime);
                uiPosition += leashTarget;
            }

            uiWorldPosition = cameraTransform.TransformPoint(uiPosition);

            // Constrain Y to fixed range
            if (constrainY)
                uiWorldPosition.y = Mathf.Clamp(uiWorldPosition.y,
                    constrainYMinMax.x + cameraPosition.y + TargetPosition.y,
                    constrainYMinMax.y + cameraPosition.y + TargetPosition.y);

            transform.position = uiWorldPosition;
            UpdateRotation(uiWorldPosition - cameraPosition);
        }

        private void OnEnable()
        {
            // Jump to the target position
            var cameraTransform = CameraRig.Instance.CenterEyeAnchor;
            var uiPosition = cameraTransform.TransformPoint(TargetPosition);
            transform.position = cameraTransform.TransformPoint(TargetPosition);
            UpdateRotation(uiPosition - cameraTransform.position);
        }

        private void UpdateRotation(Vector3 forward)
        {
            var targetRotation = Quaternion.LookRotation(forward, Vector3.up).eulerAngles;
            if (lockPitch) targetRotation.x = 0.0f;
            transform.eulerAngles = targetRotation;
        }

        private bool IsPointOutsideExtents(Vector3 point, Vector3 extents, out Vector3 axesOutsideExtents)
        {
            axesOutsideExtents = new Vector3(Mathf.Abs(point.x) > extents.x ? 1.0f : 0.0f,
                Mathf.Abs(point.y) > extents.y ? 1.0f : 0.0f,
                Mathf.Abs(point.z) > extents.z ? 1.0f : 0.0f);
            return Mathf.Abs(point.x) > extents.x
                   || Mathf.Abs(point.y) > extents.y
                   || Mathf.Abs(point.z) > extents.z;
        }

        private Vector3 Sign(Vector3 v)
        {
            return new Vector3(Mathf.Sign(v.x), Mathf.Sign(v.y), Mathf.Sign(v.z));
        }

        private Vector3 GetConstrainedRecenterTarget(Vector3 target, Vector3 original)
        {
            return new Vector3(recenterX ? target.x : original.x, recenterY ? target.y : original.y,
                recenterZ ? target.z : original.z);
        }

        private Vector3 GetScaledExtents(Vector3 relativeSize)
        {
            var scaledUiSize = Vector3.Scale(GetUISize(), transform.lossyScale);
            return Vector3.Scale(relativeSize, scaledUiSize) * 0.5f;
        }

        private Vector3 GetUISize()
        {
            return _rectTransform
                ? new Vector3(_rectTransform.rect.width, _rectTransform.rect.height, 1.0f)
                : Vector3.one;
        }

#if UNITY_EDITOR

        private void OnValidate()
        {
            SoftLeashSize = softLeashSize;
            HardLeashSize = hardLeashSize;
            RecenterTriggerSize = recenterTriggerSize;
            TargetPosition = targetPosition;
            ConstrainYMinMax = constrainYMinMax;
        }

        private void OnDrawGizmosSelected()
        {
            var lastColor = Gizmos.color;
            var lastMatrix = Gizmos.matrix;

            Gizmos.matrix = transform.localToWorldMatrix;

            _rectTransform = gameObject.GetComponent<RectTransform>();

            Gizmos.color = Color.green;
            var softLeash = Vector3.Scale(softLeashSize, GetUISize());
            Gizmos.DrawWireCube(Vector3.zero, softLeash);
            Gizmos.color = Color.yellow;
            var recenterTrigger = Vector3.Scale(recenterTriggerSize, GetUISize());
            Gizmos.DrawWireCube(Vector3.zero, recenterTrigger);
            Gizmos.color = Color.red;
            var hardLeash = Vector3.Scale(hardLeashSize, GetUISize());
            Gizmos.DrawWireCube(Vector3.zero, hardLeash);

            Gizmos.color = lastColor;
            Gizmos.matrix = lastMatrix;
        }
#endif
    }
}
