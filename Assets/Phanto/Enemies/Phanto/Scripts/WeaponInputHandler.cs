// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Phanto
{
    /// <summary>
    ///     Weapon input handler
    /// </summary>
    public class WeaponInputHandler : MonoBehaviour
    {
        public OVRInput.RawAxis1D triggerAxis;
        public int mouseButton;

        [SerializeField] private Weapon _weapon;

        [SerializeField] private Transform _handAnchorTransform;

        private bool _isTriggerHeld;

        public Weapon ControlledWeapon
        {
            get => _weapon;
            set
            {
                _weapon = value;
                if (_isTriggerHeld) _weapon.StartFiring();
            }
        }

        private void Update()
        {
            UpdatePosition();

            var isTriggerHeldThisFrame = OVRInput.Get(triggerAxis) > 0.5f || Input.GetMouseButton(mouseButton);

            if (isTriggerHeldThisFrame == _isTriggerHeld) return;

            if (_weapon)
            {
                if (isTriggerHeldThisFrame)
                    _weapon.StartFiring();
                else
                    _weapon.StopFiring();
            }

            _isTriggerHeld = isTriggerHeldThisFrame;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_weapon == null) _weapon = GetComponentInChildren<Weapon>();
        }
#endif

        public void SetHand(int hand)
        {
            mouseButton = hand;
            triggerAxis = hand == 0 ? OVRInput.RawAxis1D.LIndexTrigger : OVRInput.RawAxis1D.RIndexTrigger;
        }

        public void SetHandAnchorTransform(Transform targetTransform)
        {
            _handAnchorTransform = targetTransform;
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (_handAnchorTransform == null) return;
            transform.SetPositionAndRotation(_handAnchorTransform.position, _handAnchorTransform.rotation);
        }
    }
}
