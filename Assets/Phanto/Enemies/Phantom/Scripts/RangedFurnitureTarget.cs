// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using Phanto.Enemies.DebugScripts;
using Phantom;
using PhantoUtils;
using UnityEngine;
using UnityEngine.Assertions;
using Utilities.XR;

/// <summary>
///     This is for furniture that isn't walkable (lamp, plant, etc.)
///     Phantoms will approach these targets and perform a ranged attack to ignite them.
/// </summary>
public class RangedFurnitureTarget : PhantomTarget
{
    private readonly List<Collider> _colliders = new();
    private bool _active = true;

    public override bool Flee => false;

    public override Vector3 Position
    {
        get => transform.position;
        set => transform.position = value;
    }

    public override bool Valid => _active;

    private void OnEnable()
    {
        _active = true;
        Register(this, _colliders);
        DebugDrawManager.DebugDrawEvent += DebugDraw;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _active = false;
        Unregister(this, _colliders);
        DebugDrawManager.DebugDrawEvent -= DebugDraw;
    }

    public void Initialize(OVRSemanticClassification classification)
    {
        gameObject.SetSuffix($"{classification.Labels[0]}_{(ushort)gameObject.GetInstanceID():X4}");
        classification.GetComponentsInChildren(true, _colliders);
        Register(this, _colliders);
    }

    private Vector3 RandomPointOnCollider()
    {
        Assert.IsFalse(_colliders.Count == 0);
        var collider = _colliders.RandomElement();
        return collider.ClosestPoint(collider.bounds.RandomPoint());
    }

    public override Vector3 GetAttackPoint()
    {
        return RandomPointOnCollider();
    }

    public override void TakeDamage(float f)
    {
        // disable this target for a few (~10?) seconds
        StartCoroutine(SleepForSeconds(Random.Range(8.0f, 12.0f)));
    }

    private IEnumerator SleepForSeconds(float seconds)
    {
        _active = false;
        yield return new WaitForSeconds(seconds);
        ;
        _active = true;
    }

    public override Vector3 GetDestination(Vector3 origin)
    {
        var randomPoint = RandomPointOnCollider();
        var triangle = NavMeshBookKeeper.ClosestTriangleOnCircle(randomPoint, 0.75f, true);
        var point = triangle.GetRandomPoint();

        // return a point that's within ranged attack distance of furniture.
        return point;
    }

    public override void Show(bool visible = true)
    {
    }

    private void DebugDraw()
    {
        foreach (var collider in _colliders)
        {
            if (collider is BoxCollider boxCollider)
            {
                var boxTransform = boxCollider.transform;

                var scale = boxTransform.lossyScale;
                var size = boxCollider.size;
                size.Set(size.x * scale.x, size.y * scale.y, size.z * scale.z);

                XRGizmos.DrawWireCube(boxTransform.TransformPoint(boxCollider.center), boxTransform.rotation, size,
                    _active ? MSPalette.Orange : Color.black);

            }
            else
            {
                var bounds = collider.bounds;
                XRGizmos.DrawWireCube(bounds.center, Quaternion.identity, bounds.size,
                    _active ? MSPalette.Orange : Color.black);
            }

        }
    }
}
