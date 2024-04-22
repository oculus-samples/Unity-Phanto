// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace PhantoUtils
{
    public static class ListExtensions
    {
        public static T RandomElement<T>(this IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0) return default;

            return list[Random.Range(0, list.Count)];
        }

        public static bool ContainsElement(this IList<string> list, string element,
            StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
        {
            foreach (var item in list)
                if (item.Equals(element, comparison))
                    return true;

            return false;
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            var count = list.Count;
            // Fisher-Yates shuffle.
            for (var i = count - 1; i > 0; i--)
            {
                var randomIndex = Random.Range(0, i + 1);

                // swap elements
                (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
            }
        }
    }
}
