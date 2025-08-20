// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;
using Utilities.XR;

public class DebugPath
{
    private Vector3[] _points;
    private MonoBehaviour _owner;
    private int _count;
    private Vector3 _destination;

    public DebugPath(int count, Vector3[] corners, Vector3 destination, MonoBehaviour owner)
    {
        _count = count;
        _owner = owner;
        _destination = destination;

        _points = new Vector3[count];

        Array.Copy(corners, 0, _points, 0, count);
    }

    public void DebugDraw(Color color)
    {
        XRGizmos.DrawPointSet(_points, color, 0.05f, _count - 1);
        XRGizmos.DrawLineList(_points, color);
        XRGizmos.DrawPoint(_points[_count - 1], color);
        XRGizmos.DrawWireSphere(_destination, Quaternion.identity, 0.03f, color);
    }
}
