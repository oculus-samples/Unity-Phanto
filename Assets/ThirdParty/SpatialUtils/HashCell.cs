// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public readonly struct HashCell : IEquatable<HashCell>
{
    public readonly int x;
    public readonly int y;
    public readonly int z;

    private HashCell(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public bool Equals(HashCell other)
    {
        return x == other.x && y == other.y && z == other.z;
    }

    public override bool Equals(object obj)
    {
        return obj is HashCell other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(x, y, z);
    }

    public override string ToString()
    {
        return $"{x:000} {y:000} {z:000}";
    }

    public static HashCell GetCell(Vector3 position, float cellSize)
    {
        (int x, int y, int z) = GetCellCoords(position, cellSize);

        return new HashCell(x, y, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int x, int y, int z) GetCellCoords(Vector3 position, float cellSize)
    {
        var x = (int)Math.Round(position.x / cellSize, MidpointRounding.AwayFromZero);
        var y = (int)Math.Round(position.y / cellSize, MidpointRounding.AwayFromZero);
        var z = (int)Math.Round(position.z / cellSize, MidpointRounding.AwayFromZero);

        return (x, y, z);
    }

    public static HashCell GetCell(int x, int y, int z)
    {
        return new HashCell(x, y, z);
    }

    public static HashCell GetCell(Vector3Int v)
    {
        return new HashCell(v.x, v.y, v.z);
    }
}
