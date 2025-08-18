// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using Meta.XR.MRUtilityKit;
using Phantom.Environment.Scripts;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// SceneQuery components need to be attached to each loaded room at initialization.
/// </summary>
[RequireComponent(typeof(SceneDataLoader))]
public class SetupSceneQueries : MonoBehaviour
{
    [SerializeField] private SceneDataLoader sceneDataLoader;

    private void Awake()
    {
        FindDependencies();
    }

    private void OnEnable()
    {
        sceneDataLoader.SceneDataLoaded.AddListener(OnSceneDataLoaded);
    }

    private void OnDisable()
    {
        sceneDataLoader.SceneDataLoaded.RemoveListener(OnSceneDataLoaded);
    }

    /// <summary>
    /// Needs to be invoked from the SceneDataLoader SceneDataLoaded event
    /// </summary>
    /// <param name="root"></param>
    private void OnSceneDataLoaded(Transform root)
    {
        var rooms = root.GetComponentsInChildren<MRUKRoom>(true);

        Assert.IsFalse(rooms.Length == 0);

        foreach (var room in rooms)
        {
            if (!room.TryGetComponent<SceneQuery>(out var sceneQuery))
            {
                sceneQuery = room.gameObject.AddComponent<SceneQuery>();
            }
            sceneQuery.Initialize();
        }
    }

    private void FindDependencies()
    {
        if (sceneDataLoader == null)
        {
            sceneDataLoader = GetComponent<SceneDataLoader>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        FindDependencies();
    }
#endif
}
