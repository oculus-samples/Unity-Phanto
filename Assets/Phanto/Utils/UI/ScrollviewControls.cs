// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PhantoUtils.VR
{
    public class ScrollviewControls : MonoBehaviour
    {
        private const float InputDeadzone = 0.05f;

        [SerializeField] private float scrollSpeed = 500.0f;

        [SerializeField] private ScrollRect scrollRect;

        public UnityEvent OnScroll = new();

        private RayInteractable _rayInteractable;

        private void Awake()
        {
            Assert.IsNotNull(InteractionRig.Instance, $"{nameof(InteractionRig.Instance)} cannot be null.");
        }

        private void Update()
        {
            var scrollInput = GetScrollInputVector();

            if (!scrollRect.horizontal) scrollInput.x = 0.0f;

            if (!scrollRect.vertical) scrollInput.y = 0.0f;

            if (scrollInput.sqrMagnitude > InputDeadzone * InputDeadzone)
            {
                scrollRect.content.transform.localPosition -= new Vector3(scrollInput.x, scrollInput.y, 0.0f) *
                                                              (scrollSpeed * Time.deltaTime);
                scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(scrollRect.horizontalNormalizedPosition);
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
                OnScroll?.Invoke();
            }
        }

        private void OnEnable()
        {
            if (ApplicationUtils.IsDesktop())
            {
                enabled = false;
                return;
            }

            _rayInteractable = GetComponentInParent<RayInteractable>();
            if (_rayInteractable == null)
            {
                Debug.LogError(
                    $"{nameof(ScrollviewControls)}: A {nameof(RayInteractable)} must be an ancestor of the component. The component will be disabled.");
                enabled = false;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!scrollRect) scrollRect = GetComponent<ScrollRect>();
        }
#endif

        private Vector2 GetScrollInputVector()
        {
            var scrollInput = Vector2.zero;

            var leftRayInteractor =
                InteractionRig.Instance.GetRayInteractor(InteractionRig.InteractorType.LeftControllerInteractor);
            var rightRayInteractor =
                InteractionRig.Instance.GetRayInteractor(InteractionRig.InteractorType.RightControllerInteractor);

            if (leftRayInteractor != null && _rayInteractable.IsInteractorHovering(leftRayInteractor))
                scrollInput += NormalizeInput(OVRInput.Get(OVRInput.RawAxis2D.LThumbstick), InputDeadzone);

            if (rightRayInteractor != null && _rayInteractable.IsInteractorHovering(rightRayInteractor))
                scrollInput += NormalizeInput(OVRInput.Get(OVRInput.RawAxis2D.RThumbstick), InputDeadzone);

            return Vector2.ClampMagnitude(scrollInput, 1.0f);
        }

        private static float NormalizeInput(float value, float deadzone)
        {
            if (Mathf.Abs(value) <= deadzone) return 0.0f;
            return Mathf.Clamp((value - deadzone) / (1 - deadzone), -1.0f, 1.0f);
        }

        private static Vector2 NormalizeInput(Vector2 value, float deadzone)
        {
            return new Vector2(NormalizeInput(value.x, deadzone), NormalizeInput(value.y, deadzone));
        }
    }
}
