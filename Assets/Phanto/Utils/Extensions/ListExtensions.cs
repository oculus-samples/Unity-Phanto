// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace PhantoUtils
{
    public static class ListExtensions
    {
        public static T RandomElement<T>(this IList<T> list)
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
    }
}
