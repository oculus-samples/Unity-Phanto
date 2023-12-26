// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Assertions;
using Utilities.XR;

public class OcclusionScannerController : MonoBehaviour
{
    private const string DepthScale = "_DepthScale";
    private const string RightEye = "_RightEye";
    private const string ShaderName = "MR/OcclusionScanner";

    private static readonly int DepthScaleId = Shader.PropertyToID(DepthScale);
    private static readonly int RightEyeId = Shader.PropertyToID(RightEye);

    [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.LTouch;
    [SerializeField] private MeshRenderer meshRenderer;

    private Material _occlusionMaterial;
    private float _depthScale = -2.0f;
    private bool _rightEye = false;

    private readonly Stopwatch _eyeDisplayStopwatch = Stopwatch.StartNew();
    private readonly Stopwatch _scaleDisplayStopwatch = Stopwatch.StartNew();
    private Transform _transform;

    private void Awake()
    {
        _transform = transform;
        _occlusionMaterial = meshRenderer.material;

        // Code only works on the OcclusionScanner shader.
        Assert.IsTrue(_occlusionMaterial.shader.name.Equals(ShaderName, StringComparison.InvariantCultureIgnoreCase));
    }

    private void OnDestroy()
    {
        if (_occlusionMaterial != null)
        {
            Destroy(_occlusionMaterial);
        }
    }

    private void Update()
    {
        bool gripIsDown = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, controller);

        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickLeft, controller))
        {
            ModifyDepthScale(gripIsDown ? -0.01f : -0.1f);
        }
        else if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickRight, controller))
        {
            ModifyDepthScale(gripIsDown ? 0.01f : 0.1f);
        }

        if (OVRInput.GetDown(OVRInput.Button.Two, controller))
        {
            _rightEye = !_rightEye;

            _occlusionMaterial.SetInt(RightEyeId, _rightEye ? 1 : 0);
            _eyeDisplayStopwatch.Restart();
        }

        if (_eyeDisplayStopwatch.ElapsedMilliseconds < 2000)
        {
            XRGizmos.DrawString(_rightEye ? "RIGHT" : "LEFT", _transform.position, _transform.rotation, Color.grey, 0.05f);
        }

        if (_scaleDisplayStopwatch.ElapsedMilliseconds < 2000)
        {
            XRGizmos.DrawString(_depthScale.ToString("F2"), _transform.position - (_transform.up * 0.15f), _transform.rotation, Color.grey, 0.05f);
        }
    }

    private void ModifyDepthScale(float amount)
    {
        _depthScale = Mathf.Clamp(_depthScale + amount, -2, 2);
        _occlusionMaterial.SetFloat(DepthScaleId, _depthScale);

        _scaleDisplayStopwatch.Restart();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
    }
#endif
}
