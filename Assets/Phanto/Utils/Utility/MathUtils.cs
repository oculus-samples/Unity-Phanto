// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Runtime.CompilerServices;
using UnityEngine;

public static class MathUtils
{
    /// <summary>Returns the result of a non-clamping linear remapping of a value x from source range [a, b] to the destination range [c, d].</summary>
    /// <param name="a">The first endpoint of the source range [a,b].</param>
    /// <param name="b">The second endpoint of the source range [a, b].</param>
    /// <param name="c">The first endpoint of the destination range [c, d].</param>
    /// <param name="d">The second endpoint of the destination range [c, d].</param>
    /// <param name="x">The value to remap from the source to destination range.</param>
    /// <returns>The remap of input x from the source range to the destination range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Remap(float a, float b, float c, float d, float x)
    {
        // Mathf version of method from Unity.Mathematics
        return Mathf.Lerp(c, d, Mathf.InverseLerp(a, b, x));
    }
}
