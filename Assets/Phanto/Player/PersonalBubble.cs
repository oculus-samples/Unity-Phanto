// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Phanto.Enemies.DebugScripts;
using PhantoUtils;
using UnityEngine;
using Utilities.XR;

/// <summary>
/// Positions a capsule over the player's head and torso
/// to keep Phanto out of the player's personal space.
/// </summary>
public class PersonalBubble : MonoBehaviour
{
    private static readonly Dictionary<Object, PersonalBubble> PlayerBubbles = new();

    [SerializeField][Range(0.25f,1.0f)] private float radius = 0.33f;

    [Tooltip("The camera rig to reference")]
    [SerializeField] private OVRCameraRig cameraRig;

    [Tooltip("The collider this obstacle is attached to")]
    [SerializeField] private new CapsuleCollider collider;

    [SerializeField] private bool debug;

    private Transform _transform;

    public float Radius
    {
        get => radius;
        set
        {
            radius = value;
            SetCapsuleRadius(value);
        }
    }

    private void Awake()
    {
        _transform = transform;
    }

    private void OnEnable()
    {
        cameraRig.UpdatedAnchors += OnUpdatedAnchors;

        DebugDrawManager.DebugDrawEvent += DebugDraw;

        PlayerBubbles[collider] = this;
    }

    private void OnDisable()
    {
        cameraRig.UpdatedAnchors -= OnUpdatedAnchors;

        DebugDrawManager.DebugDrawEvent -= DebugDraw;

        PlayerBubbles.Remove(collider);
    }

    private void OnUpdatedAnchors(OVRCameraRig rig)
    {
        _transform.position = rig.centerEyeAnchor.position;
    }

    public static bool IsPlayerBubble(Object other)
    {
        return PlayerBubbles.ContainsKey(other);
    }

    private void DebugDraw()
    {
        if (debug) XRGizmos.DrawCollider(collider, MSPalette.SkyBlue);
    }

    private void SetCapsuleRadius(float newRadius)
    {
        collider.radius = newRadius;

        var center = collider.center;
        var halfHeight = collider.height * 0.5f;

        center.y = newRadius - halfHeight;
        collider.center = center;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (collider == null)
        {
            collider = GetComponent<CapsuleCollider>();
        }

        SetCapsuleRadius(radius);
    }
#endif
}
