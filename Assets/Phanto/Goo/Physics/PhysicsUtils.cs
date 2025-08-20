// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Phanto
{
    public static class PhysicsUtils
    {
        public const int MAX_RAYCAST_RESULTS = 256;
        public const int MAX_COLLIDER_RESULTS = 256;

        public const int MAX_CONTACT_POINTS = 256;
        public static RaycastHit[] raycastResults = new RaycastHit[MAX_RAYCAST_RESULTS];
        public static Collider[] colliderResults = new Collider[MAX_COLLIDER_RESULTS];
        private static readonly Dictionary<GameObject, Tuple<float, int>> goMap = new(256);
        public static ContactPoint[] contactPoints = new ContactPoint[MAX_CONTACT_POINTS];

        private static void Swap<T>(T[] arr,
            int index1,
            int index2)
        {
            var tmp = arr[index2];
            arr[index2] = arr[index1];
            arr[index1] = tmp;
        }

        public static int DeduplicateHits(Vector3 point,
            Collider[] colliders,
            int numCollisions)
        {
            for (var i = 0;
                 i < numCollisions;
                 ++i)
            {
                var c = colliders[i];
                var go = c.attachedRigidbody == null ? c.gameObject : c.attachedRigidbody.gameObject;
                var dist = c.ClosestPointOnBounds(point).sqrMagnitude;
                Tuple<float, int> closest;
                if (goMap.TryGetValue(go, out closest))
                {
                    if (dist < closest.Item1)
                    {
                        Swap(colliders, i, closest.Item2);
                        Swap(colliders, i, numCollisions - 1);
                        goMap[go] = new Tuple<float, int>(dist, i);
                        --numCollisions;
                        --i;
                    }
                }
                else
                {
                    goMap[go] = new Tuple<float, int>(dist, i);
                }
            }

            goMap.Clear();

            return numCollisions;
        }

        public static int DeduplicateHits(RaycastHit[] raycastHits,
            int numHits)
        {
            for (var i = 0;
                 i < numHits;
                 ++i)
            {
                var hit = raycastHits[i];
                var go = hit.rigidbody == null ? hit.collider.gameObject : hit.rigidbody.gameObject;
                Tuple<float, int> closest;
                if (goMap.TryGetValue(go, out closest))
                {
                    if (hit.distance < closest.Item1)
                    {
                        Swap(raycastHits, i, closest.Item2);
                        Swap(raycastHits, i, numHits - 1);
                        goMap[go] = new Tuple<float, int>(hit.distance, i);
                        --numHits;
                        --i;
                    }
                }
                else
                {
                    goMap[go] = new Tuple<float, int>(hit.distance, i);
                }
            }

            goMap.Clear();

            return numHits;
        }

        public static Vector3 GetAverageContact(Collision c)
        {
            var avgContact = new Vector3();
            var count = c.GetContacts(contactPoints);
            var rcpCount = 1f / count;
            for (var i = count - 1; i >= 0; --i)
            {
                avgContact += rcpCount * contactPoints[i].point;
            }

            return avgContact;
        }
    }
}
