// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Phanto;
using Phanto.Enemies.DebugScripts;
using Phantom;
using PhantoUtils;
using UnityEngine;
using UnityEngine.AI;
using Utilities.XR;

/// <summary>
///     Chases a target with physics.
/// </summary>
public class PhantomChaseTarget : PhantomTarget
{
    [SerializeField] private GameObject gooPrefab;
    [SerializeField] private float lifeSpan = 10.0f;

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

    private void OnEnable()
    {
        Register(this, _colliders);
        StartCoroutine(LifetimeTimer(lifeSpan));
        DebugDrawManager.DebugDrawEvent += DebugDraw;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Unregister(this, _colliders);
        PhantomManager.Instance.ReturnToPool(this);
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

    public override void TakeDamage(float f)
    {
        PoolManagerSingleton.Instance.Create(gooPrefab, Position, Quaternion.LookRotation(Vector3.up));
        Hide();
    }

    public override Vector3 GetAttackPoint()
    {
        return transform.position;
    }

    public override Vector3 GetDestination(Vector3 point)
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

    private IEnumerator LifetimeTimer(float duration)
    {
        // if target can't be reached after X seconds it should go back into pool.
        yield return new WaitForSeconds(duration);
        Hide();
    }

    private void DebugDraw()
    {
        XRGizmos.DrawSphere(transform.position + new Vector3(0f, 0.08f, 0f), 0.08f, MSPalette.Orange);
    }
}
