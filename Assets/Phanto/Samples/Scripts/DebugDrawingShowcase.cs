// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Phanto.Enemies.DebugScripts;
using Phantom.Environment.Scripts;
using PhantoUtils;
using PhantoUtils.VR;
using Tayx.Graphy;
using UnityEngine;
using Utilities.XR;
using static NavMeshConstants;

public class DebugDrawingShowcase : MonoBehaviour
{
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;

    [SerializeField] private bool debugDraw = true;

    private bool _sceneReady;
    private bool _started;

    private Transform _head;

    private GraphyManager _graphyManager;

    protected void Awake()
    {
        DebugDrawManager.DebugDraw = debugDraw;
    }

    private IEnumerator Start()
    {
        while (CameraRig.Instance == null)
        {
            yield return null;
        }

        _head = CameraRig.Instance.CenterEyeAnchor;

        do
        {
            _graphyManager = FindObjectOfType<GraphyManager>(true);
        } while (_graphyManager == null);

        _graphyManager.gameObject.SetActive(true);

        _started = true;
    }

    private void OnEnable()
    {
        SceneBoundsChecker.BoundsChanged += OnBoundsChanged;
        DebugDrawManager.DebugDrawEvent += DebugDraw;
    }

    private void OnDisable()
    {
        SceneBoundsChecker.BoundsChanged -= OnBoundsChanged;
        DebugDrawManager.DebugDrawEvent -= DebugDraw;
    }

    private void DebugDraw()
    {
        if (!_sceneReady || !_started)
        {
            return;
        }

        if (!_graphyManager.isActiveAndEnabled)
        {
            _graphyManager.gameObject.SetActive(true);
        }

        var leftPos = leftHand.position;
        var rightPos = rightHand.position;

        XRGizmos.DrawPointer(leftPos, leftHand.forward, Color.blue, 0.2f);
        XRGizmos.DrawPointer(rightPos, rightHand.forward, Color.red, 0.2f);

        XRGizmos.DrawWireSphere(leftPos, leftHand.rotation, 0.075f, MSPalette.DodgerBlue);
        XRGizmos.DrawWireCube(rightPos, rightHand.rotation, new Vector3(0.05f, 0.2f, 0.1f),
            MSPalette.MediumVioletRed);

        var ray = new Ray(leftPos, leftHand.forward);

        var leftHitPos = DrawRayHit(ray, true);

        ray = new Ray(rightPos, rightHand.forward);

        var rightHitPos = DrawRayHit(ray, false);

        // Draw line and distance between controllers
        var delta = leftPos - rightPos;
        var distance = delta.magnitude;
        var midPoint = Vector3.Lerp(leftPos, rightPos, 0.5f);
        var towardsHead = Vector3.ProjectOnPlane(midPoint - _head.position, Vector3.up).normalized;
        var textUp = Vector3.Cross(delta.normalized, towardsHead);
        var textRotation = Quaternion.LookRotation(towardsHead, textUp);

        XRGizmos.DrawLine(leftPos, rightPos, Color.yellow);
        XRGizmos.DrawString($"{distance:F2}m", midPoint + new Vector3(0, 0.02f, 0),
            textRotation, MSPalette.SlateGray, 0.02f, 0.05f);

        // Draw line and distance between ray hits
        if (leftHitPos.HasValue && rightHitPos.HasValue)
        {
            leftPos = leftHitPos.Value;
            rightPos = rightHitPos.Value;

            // Draw line and distance between controllers
            delta = leftPos - rightPos;
            distance = delta.magnitude;
            midPoint = Vector3.Lerp(leftPos, rightPos, 0.5f);
            towardsHead = Vector3.ProjectOnPlane(midPoint - _head.position, Vector3.up).normalized;
            textUp = Vector3.Cross(delta.normalized, towardsHead);
            textRotation = Quaternion.LookRotation(towardsHead, textUp);

            XRGizmos.DrawLine(leftPos, rightPos, MSPalette.DarkGoldenrod);
            XRGizmos.DrawString($"{distance:F2}m", midPoint + new Vector3(0, 0.02f, 0),
                textRotation, MSPalette.MediumTurquoise, 0.05f, 0.1f, 0.005f);
        }
    }

    private void OnBoundsChanged(Bounds bounds)
    {
        _sceneReady = true;
    }

    private Vector3? DrawRayHit(Ray ray, bool left)
    {
        if (!Physics.Raycast(ray, out var hit, 100.0f, SceneMeshLayerMask, QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        var position = hit.point;
        var normal = hit.normal;

        var rotation = Quaternion.FromToRotation(Vector3.up, normal);

        XRGizmos.DrawCircle(position, rotation, 0.1f, left ? MSPalette.Blue : MSPalette.Red);

        var angle = Vector3.Angle(Vector3.up, normal);
        var pointerColor = GetPointerColor(angle);
        XRGizmos.DrawPointer(position, normal, pointerColor, 0.15f, 0.005f);

        Color GetPointerColor(float angle)
        {
            if (angle > 30 && angle < 120) return MSPalette.Yellow;

            if (angle >= 120) return MSPalette.Red;

            return MSPalette.Lime;
        }

        return position;
    }
}
