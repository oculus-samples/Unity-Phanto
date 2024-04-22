// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
public class TableCrystal : Crystal
{
    [SerializeField] private CrystalChaseTarget crystalChaseTarget;

    public CrystalChaseTarget CrystalChaseTarget => crystalChaseTarget;

    public bool Valid => crystalChaseTarget.Valid;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (crystalChaseTarget == null)
        {
            crystalChaseTarget = GetComponent<CrystalChaseTarget>();
        }
    }
#endif
}
