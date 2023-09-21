// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Linq;
using UnityEngine.SceneManagement;

namespace PhantoUtils
{
    public static class SceneUtils
    {
        public static T[] FindComponentsOfType<T>(bool includeInactive = false)
        {
            return SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(el => el.GetComponentsInChildren<T>(includeInactive)).ToArray();
        }
    }
}
