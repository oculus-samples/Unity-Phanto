// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using PhantoUtils.VR;
using UnityEngine;

public class SoftParent : MonoBehaviour
{
    public enum ParentTargetType
    {
        Camera,
        LeftHand,
        LeftController,
        RightHand,
        RightController,
        Transform
    }

    [SerializeField] private ParentTargetType parentTarget;

    [SerializeField] private Transform parentTransform;

    [SerializeField] private Vector3 offset;

    private Transform _targetTransform;

    public ParentTargetType ParentTarget
    {
        get => parentTarget;
        set
        {
            parentTarget = value;
            FindTargetTransform();
        }
    }

    public Transform TargetTransform
    {
        get => _targetTransform;
        set
        {
            parentTransform = value;
            _targetTransform = value;
            parentTarget = ParentTargetType.Transform;
        }
    }

    private void Awake()
    {
        _targetTransform = FindTargetTransform();
    }

    private void Update()
    {
        if (_targetTransform == null)
        {
            // We could check this in Start, but checking on first update allows for late transform attaching
            _targetTransform = FindTargetTransform();
            if (_targetTransform == null) enabled = false;
        }

        transform.position = _targetTransform.TransformPoint(offset);
        transform.rotation = _targetTransform.rotation;
    }

    private Transform FindTargetTransform()
    {
        switch (parentTarget)
        {
            case ParentTargetType.Camera:
                return CameraRig.Instance != null ? CameraRig.Instance.CenterEyeAnchor : null;
            case ParentTargetType.LeftHand:
                return CameraRig.Instance != null ? CameraRig.Instance.LeftHandAnchor : null;
            case ParentTargetType.LeftController:
                return CameraRig.Instance != null ? CameraRig.Instance.LeftControllerAnchor : null;
            case ParentTargetType.RightHand:
                return CameraRig.Instance != null ? CameraRig.Instance.RightHandAnchor : null;
            case ParentTargetType.RightController:
                return CameraRig.Instance != null ? CameraRig.Instance.RightControllerAnchor : null;
            case ParentTargetType.Transform:
                return parentTransform;
            default:
                throw new ArgumentOutOfRangeException(nameof(parentTarget), parentTarget, null);
        }
    }
}
