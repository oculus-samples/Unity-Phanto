// Copyright (c) Meta Platforms, Inc. and affiliates.

using Phanto.Enemies.DebugScripts;
using Phantom;
using PhantoUtils;
using UnityEngine;
using UnityEngine.AI;
using Utilities.XR;

public class PhantomDemoChaseTarget : PhantomTarget
{
    private Collider[] _colliders;

    public override bool Flee => false;

    public override Vector3 Position
    {
        get => transform.position;
        set => transform.position = value;
    }

    public override bool Valid => isActiveAndEnabled;

    private void Awake()
    {
        _colliders = GetComponentsInChildren<Collider>();
    }

    protected override void OnEnable()
    {
        Register(this, _colliders);
        DebugDrawManager.DebugDrawEvent += DebugDraw;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Unregister(this, _colliders);
        DebugDrawManager.DebugDrawEvent -= DebugDraw;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var collider = GetComponentInChildren<Collider>();
        if (collider == null) return;

        var matrix = transform.localToWorldMatrix;

        Gizmos.matrix = matrix;
        Gizmos.color = MSPalette.Orange;
        var bounds = collider.bounds;

        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
#endif

    public override void Initialize(OVRSemanticClassification classification, OVRSceneRoom _)
    {
    }

    public override void TakeDamage(float f)
    {
        Hide();
    }

    public override Vector3 GetAttackPoint()
    {
        return transform.position;
    }

    public override Vector3 GetDestination(Vector3 point, float min = 0.0f, float max = 0.0f)
    {
        var destination = Position;
        if (_colliders.Length != 0) destination = _colliders[0].ClosestPoint(point);

        if (NavMesh.SamplePosition(destination, out var navMeshHit, 1.0f, NavMesh.AllAreas))
            destination = navMeshHit.position;

        return destination;
    }

    public override void Show(bool visible = true)
    {
        gameObject.SetActive(visible);
    }

    private void DebugDraw()
    {
        XRGizmos.DrawSphere(transform.position + new Vector3(0f, 0.08f, 0f), 0.08f, MSPalette.Orange);
    }
}
