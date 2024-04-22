// Copyright (c) Meta Platforms, Inc. and affiliates.

using Phanto.Audio.Scripts;
using Phanto.Enemies.DebugScripts;
using PhantoUtils;
using UnityEngine;
using Utilities.XR;

namespace Phantom.EctoBlaster.Scripts
{
    public class EctoBlasterDemoSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject blasterPrefab;
        [SerializeField] private GameObject blasterPreviewPrefab;
        [SerializeField] private LayerMask meshLayerMask;

        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        [Tooltip("The radius to start tracking the target")] [SerializeField]
        private float trackingRadius = 1.0f;

        [SerializeField] private PhantoRandomOneShotSfxBehavior placeDownSFX;
        [SerializeField] private PhantoRandomOneShotSfxBehavior pickUpSFX;

        [SerializeField] private bool debugDraw = true;

        private OVRInput.Controller _activeController = OVRInput.Controller.RTouch;

        private GameObject _blaster;
        private GameObject _blasterPreview;

        private EctoBlasterDemoRadar _blasterRadar;
        private bool _isPlaced;

        private (Vector3 point, Vector3 normal, bool hit) _leftHandHit;
        private (Vector3 point, Vector3 normal, bool hit) _rightHandHit;

        private void Start()
        {
            _blasterPreview = Instantiate(blasterPreviewPrefab, transform);
            _blaster = Instantiate(blasterPrefab, transform);
            _blaster.SetActive(false);

            _blasterRadar = _blaster.GetComponent<EctoBlasterDemoRadar>();
            _blasterRadar.TrackingRadius = trackingRadius;

            DebugDrawManager.DebugDraw = debugDraw;
        }

        private void Update()
        {
            var togglePlacement = false;
            const OVRInput.Button buttonMask = OVRInput.Button.PrimaryIndexTrigger | OVRInput.Button.PrimaryHandTrigger;

            if (OVRInput.GetDown(buttonMask, OVRInput.Controller.LTouch))
            {
                _activeController = OVRInput.Controller.LTouch;
                togglePlacement = true;
            }
            else if (OVRInput.GetDown(buttonMask, OVRInput.Controller.RTouch))
            {
                _activeController = OVRInput.Controller.RTouch;
                togglePlacement = true;
            }

            var leftRay = new Ray(leftHand.position, leftHand.forward);
            var rightRay = new Ray(rightHand.position, rightHand.forward);

            var leftRaySuccess = Physics.Raycast(leftRay, out var leftHit, 100.0f, meshLayerMask);
            var rightRaySuccess = Physics.Raycast(rightRay, out var rightHit, 100.0f, meshLayerMask);

            _leftHandHit = (leftHit.point, leftHit.normal, leftRaySuccess);
            _rightHandHit = (rightHit.point, rightHit.normal, rightRaySuccess);
            var active = _activeController == OVRInput.Controller.LTouch ? _leftHandHit : _rightHandHit;

            if (togglePlacement && active.hit) TogglePlacement(active.point, active.normal);

            if (!_isPlaced && active.hit)
            {
                // update the position of the preview to match the raycast.
                var blasterPreviewTransform = _blasterPreview.transform;

                blasterPreviewTransform.position = active.point;
                blasterPreviewTransform.up = active.normal;
            }
        }

        private void OnEnable()
        {
            DebugDrawManager.DebugDrawEvent += DebugDraw;
        }

        private void OnDisable()
        {
            DebugDrawManager.DebugDrawEvent -= DebugDraw;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_blasterRadar != null) _blasterRadar.TrackingRadius = trackingRadius;
        }
#endif

        private void TogglePlacement(Vector3 point, Vector3 normal)
        {
            if (_isPlaced)
            {
                _blaster.SetActive(false);
                _blasterPreview.SetActive(true);
                pickUpSFX.PlaySfxAtPosition(point);

                _isPlaced = false;
            }
            else
            {
                var blasterTransform = _blaster.transform;
                blasterTransform.position = point;
                blasterTransform.up = normal;

                _blaster.SetActive(true);
                _blasterPreview.SetActive(false);
                placeDownSFX.PlaySfxAtPosition(point);

                _isPlaced = true;
            }
        }

        private void DebugDraw()
        {
            Color GetPointerColor(float angle)
            {
                if (angle > 30 && angle < 120) return MSPalette.Yellow;

                if (angle >= 120) return MSPalette.Red;

                return MSPalette.Lime;
            }

            if (_leftHandHit.hit)
            {
                var position = _leftHandHit.point;
                var rotation = Quaternion.FromToRotation(Vector3.up, _leftHandHit.normal);

                XRGizmos.DrawCircle(position, rotation, 0.15f, MSPalette.Blue);

                var angle = Vector3.Angle(Vector3.up, _leftHandHit.normal);
                var pointerColor = GetPointerColor(angle);
                XRGizmos.DrawPointer(position, _leftHandHit.normal, pointerColor, 0.3f, 0.005f);
            }

            if (_rightHandHit.hit)
            {
                var position = _rightHandHit.point;
                var rotation = Quaternion.FromToRotation(Vector3.up, _rightHandHit.normal);

                XRGizmos.DrawCircle(position, rotation, 0.15f, MSPalette.Red);

                var angle = Vector3.Angle(Vector3.up, _rightHandHit.normal);
                var pointerColor = GetPointerColor(angle);
                XRGizmos.DrawPointer(position, _rightHandHit.normal, pointerColor, 0.3f, 0.005f);
            }
        }
    }
}
