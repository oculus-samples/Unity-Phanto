// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEngine;

namespace PhantoUtils
{

    public static class RigidbodyExtensions
    {
        public static void LaunchProjectile(this Rigidbody rigidbody, Vector3 position, Vector3 launchVector)
        {
#if UNITY_6000_0_OR_NEWER
            rigidbody.linearVelocity = Vector3.zero;
#else
            rigidbody.velocity = Vector3.zero;
#endif
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.position = position;
            rigidbody.isKinematic = false;
            rigidbody.AddForce(launchVector, ForceMode.VelocityChange);
        }
    }
}
