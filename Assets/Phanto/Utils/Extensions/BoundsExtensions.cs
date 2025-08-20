// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
using UnityEngine.Assertions;

namespace PhantoUtils
{
    public static class BoundsExtensions
    {
        /// <summary>
        ///     Returns a random point inside of the supplied bounds.
        /// </summary>
        /// <returns></returns>
        public static Vector3 RandomPoint(this Bounds b)
        {
            var e = b.extents;
            var point = new Vector3(Random.Range(-e.x, e.x), Random.Range(-e.y, e.y), Random.Range(-e.z, e.z)) +
                        b.center;

            Assert.IsTrue(b.Contains(point));

            return point;
        }
    }
}
