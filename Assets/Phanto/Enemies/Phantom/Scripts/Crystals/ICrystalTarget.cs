// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICrystalTarget
{
    // nearly empty interface so we can quickly find crystals in a list
    // regardless of what they inherit from.

    Vector3 Position { get; }
    bool Valid { get; }
}
