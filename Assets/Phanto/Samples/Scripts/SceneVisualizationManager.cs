// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using Utilities.XR;
using Phanto.Enemies.DebugScripts;
using Phantom.Environment.Scripts;
using PhantoUtils;
using PhantoUtils.VR;
using UnityEngine;
using static NavMeshConstants;

namespace Phantom
{
    public class SceneVisualizationManager : MonoBehaviour
    {
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        [SerializeField] private bool debugDraw = true;

        public static Action<bool> ShowWireframe;

        private readonly Dictionary<Transform, OVRSemanticClassification> _sceneClassifications = new Dictionary<Transform, OVRSemanticClassification>();

        private bool _sceneReady;
        private bool _started;

        private Transform _head;
        private int _layerMask;
        private bool _meshVisible = true;

        protected void Awake()
        {
            _layerMask = DefaultLayerMask | SceneMeshLayerMask;
            DebugDrawManager.DebugDraw = debugDraw;
        }

        private IEnumerator Start()
        {
            while (CameraRig.Instance == null)
            {
                yield return null;
            }

            _head = CameraRig.Instance.CenterEyeAnchor;

            ShowWireframe?.Invoke(_meshVisible);
            _started = true;
        }

        private void Update()
        {
            if (!_sceneReady || !_started)
            {
                return;
            }

            if (OVRInput.GetDown(OVRInput.Button.One | OVRInput.Button.Three, OVRInput.Controller.LTouch | OVRInput.Controller.RTouch))
            {
                // toggle the wireframe.
                _meshVisible = !_meshVisible;
                ShowWireframe?.Invoke(_meshVisible);
            }

            XRGizmos.DrawPointer(leftHand.position, leftHand.forward, Color.blue, 0.2f);
            XRGizmos.DrawPointer(rightHand.position, rightHand.forward, Color.red, 0.2f);

            var ray = new Ray(leftHand.position, leftHand.forward);

            TestSceneObjects(ray);

            ray = new Ray(rightHand.position, rightHand.forward);

            TestSceneObjects(ray);
        }

        private void OnEnable()
        {
            SceneBoundsChecker.BoundsChanged += OnBoundsChanged;
        }

        private void OnDisable()
        {
            SceneBoundsChecker.BoundsChanged -= OnBoundsChanged;
        }

        private void OnBoundsChanged(Bounds bounds)
        {
            _sceneReady = true;
        }

        private readonly RaycastHit[] _raycastHits = new RaycastHit[256];

        private void TestSceneObjects(Ray ray)
        {
            var count = Physics.RaycastNonAlloc(ray, _raycastHits, 100.0f, _layerMask, QueryTriggerInteraction.Ignore);

            RaycastHit? globalMeshHit = null;
            Transform closest = null;
            Vector3 closestPoint = default;
            float closestDistance = float.MaxValue;
            OVRSemanticClassification closestClassification = null;

            for (int i = 0; i < count; i++)
            {
                var hit = _raycastHits[i];

                if (!_sceneClassifications.TryGetValue(hit.transform, out var classification))
                {
                    if (!hit.transform.TryGetComponent(out classification))
                    {
                        classification = hit.transform.GetComponentInParent<OVRSemanticClassification>();
                    }

                    _sceneClassifications[hit.transform] = classification;
                }

                if (classification == null)
                {
                    continue;
                }

                if (!globalMeshHit.HasValue && classification.Labels[0] == OVRSceneManager.Classification.GlobalMesh)
                {
                    globalMeshHit = hit;
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestClassification = classification;
                    closest = classification.transform;
                    closestDistance = hit.distance;
                    closestPoint = hit.point;
                }
            }

            if (closest != null)
            {
                var pos = closest.position;
                var direction = Vector3.ProjectOnPlane( pos - _head.position, Vector3.up).normalized;

                XRGizmos.DrawPoint(closestPoint, Color.white, 0.05f);
                XRGizmos.DrawAxis(closest, 0.15f, 0.006f);
                XRGizmos.DrawString(closestClassification.Labels[0], pos + new Vector3(0, 0.18f, 0), Quaternion.LookRotation(direction), Color.cyan, 0.05f, 0.1f, 0.004f);
            }

            if (globalMeshHit.HasValue)
            {
                var position = globalMeshHit.Value.point;
                var normal = globalMeshHit.Value.normal;

                var rotation = Quaternion.FromToRotation(Vector3.up, normal);

                XRGizmos.DrawCircle(position, rotation, 0.1f, MSPalette.Red);

                var angle = Vector3.Angle(Vector3.up, normal);
                var pointerColor = GetPointerColor(angle);
                XRGizmos.DrawPointer(position, normal, pointerColor, 0.15f, 0.005f);
            }

            Color GetPointerColor(float angle)
            {
                if (angle > 30 && angle < 120) return MSPalette.Yellow;

                if (angle >= 120) return MSPalette.Red;

                return MSPalette.Lime;
            }
        }
    }
}
