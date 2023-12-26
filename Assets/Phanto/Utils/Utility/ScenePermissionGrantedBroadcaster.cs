// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using PhantoUtils;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Events;

public class ScenePermissionGrantedBroadcaster : MonoBehaviour
{
    private const string SCENE_PERMISSION = "com.oculus.permission.USE_SCENE";

    private static readonly HashSet<string> s_GrantedPermissions = new HashSet<string>();

    private static event Action s_permissionGrantedEvent;
    public static event Action PermissionGrantedEvent
    {
        add
        {
            if (s_GrantedPermissions.Contains(SCENE_PERMISSION))
            {
                value.Invoke();
            }

            s_permissionGrantedEvent += value;
        }
        remove => s_permissionGrantedEvent -= value;
    }

    [SerializeField] private UnityEvent permissionGrantedEvent;

    private readonly WaitForSeconds _wait = new WaitForSeconds(0.1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void Initialize()
    {
        OVRPermissionsRequester.PermissionGranted += OVRPermissionsRequester_PermissionGranted;
    }

    private void Awake()
    {
        PermissionGrantedEvent += () => { permissionGrantedEvent?.Invoke(); };
    }

    private IEnumerator Start()
    {
        while (!s_GrantedPermissions.Contains(SCENE_PERMISSION))
        {
            yield return _wait;
            if (Permission.HasUserAuthorizedPermission(SCENE_PERMISSION)
                || !OcclusionKeywordToggle.SupportsOcclusion)
            {
                OVRPermissionsRequester_PermissionGranted(SCENE_PERMISSION);
                break;
            }
        }

        s_permissionGrantedEvent?.Invoke();
    }

    private static void OVRPermissionsRequester_PermissionGranted(string permission)
    {
        Debug.Log($"{nameof(ScenePermissionGrantedBroadcaster)} permission granted event: {permission}");
        s_GrantedPermissions.Add(permission);
    }
}
