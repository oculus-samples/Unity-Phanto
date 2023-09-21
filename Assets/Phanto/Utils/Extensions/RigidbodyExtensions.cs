// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace PhantoUtils
{

    public static class RigidbodyExtensions
    {
        public static void LaunchProjectile(this Rigidbody rigidbody, Vector3 position, Vector3 launchVector)
        {
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.position = position;
            rigidbody.isKinematic = false;
            rigidbody.AddForce(launchVector, ForceMode.VelocityChange);
        }
    }
}
