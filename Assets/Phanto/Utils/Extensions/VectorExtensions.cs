// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Runtime.CompilerServices;
using UnityEngine;

namespace PhantoUtils
{
    public static class VectorExtensions
    {
        private const MethodImplOptions MethodOptions = MethodImplOptions.AggressiveInlining;

        [MethodImpl(MethodOptions)]
        public static Vector2 XY(this Vector3 v)
        {
            return new Vector2(v.x, v.y);
        }

        [MethodImpl(MethodOptions)]
        public static Vector2 XZ(this Vector3 v)
        {
            return new Vector2(v.x, v.z);
        }

        [MethodImpl(MethodOptions)]
        public static Vector2 YZ(this Vector3 v)
        {
            return new Vector2(v.y, v.z);
        }

        [MethodImpl(MethodOptions)]
        public static Vector3 XYZ(this Vector4 v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        /// <summary>
        ///     Performs a componentwise division of a/b
        /// </summary>
        [MethodImpl(MethodOptions)]
        public static Vector2 DivideBy(this Vector2 v, Vector2 other)
        {
            return new Vector2(v.x / other.x, v.y / other.y);
        }

        /// <summary>
        ///     Performs a componentwise division of a/b
        /// </summary>
        [MethodImpl(MethodOptions)]
        public static Vector3 DivideBy(this Vector3 v, Vector3 other)
        {
            return new Vector3(v.x / other.x, v.y / other.y, v.z / other.z);
        }

        /// <summary>
        ///     Performs a componentwise division of a/b
        /// </summary>
        [MethodImpl(MethodOptions)]
        public static Vector4 DivideBy(this Vector4 v, Vector4 other)
        {
            return new Vector4(v.x / other.x, v.y / other.y, v.z / other.z, v.w / other.w);
        }

        /// <summary>
        ///     Are two vectors approximately equal
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        [MethodImpl(MethodOptions)]
        public static bool Approximately(this Vector3 a, Vector3 b, float epsilon = Vector3.kEpsilon)
        {
            return (a - b).sqrMagnitude <= epsilon * epsilon;
        }

        [MethodImpl(MethodOptions)]
        public static bool IsNan(this Vector3 v)
        {
            return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z);
        }

        [MethodImpl(MethodOptions)]
        public static bool IsInfinity(this Vector3 v)
        {
            return float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z);
        }

        /// <summary>
        /// If any component of a world space vector is NaN or infinity
        /// it's not going to be useful in XR.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        [MethodImpl(MethodOptions)]
        public static bool IsSafeValue(this Vector3 v)
        {
            return !v.IsNan() && !v.IsInfinity();
        }
    }
}
