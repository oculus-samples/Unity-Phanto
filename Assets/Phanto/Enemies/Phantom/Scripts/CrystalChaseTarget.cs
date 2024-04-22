// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using Phanto.Enemies.DebugScripts;
using UnityEngine;

public class CrystalChaseTarget : PhantomChaseTarget, ICrystalTarget
{
    [SerializeField] private Transform attackPoint;

    [SerializeField] private float hitPoints = 10;

    private float _currentHealth;

    protected override void OnEnable()
    {
        _currentHealth = hitPoints;

        Register(this, _colliders);
    }

    protected override void OnDisable()
    {
        Unregister(this, _colliders);
    }

    public override void TakeDamage(float f)
    {
        // decrement hit points and eventually shatter (defeat condition?)
        _currentHealth -= f;

        if (_currentHealth <= 0)
        {
            Hide();
        }
    }

    public override Vector3 GetAttackPoint()
    {
        return attackPoint.position;
    }
}
