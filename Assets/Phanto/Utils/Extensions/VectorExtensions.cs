// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace PhantoUtils
{
    public static class VectorExtensions
    {
        public static Vector2 XY(this Vector3 v)
        {
            return new Vector2(v.x, v.y);
        }

        public static Vector2 XZ(this Vector3 v)
        {
            return new Vector2(v.x, v.z);
        }

        public static Vector2 YZ(this Vector3 v)
        {
            return new Vector2(v.y, v.z);
        }

        public static Vector3 XYZ(this Vector4 v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        /// <summary>
        ///     Performs a componentwise division of a/b
        /// </summary>
        public static Vector2 DivideBy(this Vector2 v, Vector2 other)
        {
            return new Vector2(v.x / other.x, v.y / other.y);
        }

        /// <summary>
        ///     Performs a componentwise division of a/b
        /// </summary>
        public static Vector3 DivideBy(this Vector3 v, Vector3 other)
        {
            return new Vector3(v.x / other.x, v.y / other.y, v.z / other.z);
        }

        /// <summary>
        ///     Performs a componentwise division of a/b
        /// </summary>
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
        public static bool Approximately(this Vector3 a, Vector3 b, float epsilon = Vector3.kEpsilon)
        {
            return (a - b).sqrMagnitude <= epsilon * epsilon;
        }
    }
}
