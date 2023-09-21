// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Linq;
using System.Reflection;
using OVRSimpleJSON;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public static class NavMeshAgentExtensions
{
    private static readonly string[] skipFields =
    {
        "enabled",
        "isActiveAndEnabled",
        "transform",
        "gameObject",
        "tag",
        "rigidbody",
        "rigidbody2D",
        "camera",
        "light",
        "animation",
        "constantForce",
        "renderer",
        "audio",
        "networkView",
        "collider",
        "collider2D",
        "hingeJoint",
        "particleSystem",
        "hideFlags"
    };

    private static JSONObject ObjectStateJSON(this object obj)
    {
        var jsonObject = new JSONObject();
        var parms = new object[] { };
        var type = obj.GetType();

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var p in properties)
        {
            if (skipFields.Contains(p.Name)) continue;

            object result = null;

            try
            {
                result = p.GetValue(obj, parms);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = e.InnerException?.Message ?? e.Message;
            }

            switch (result)
            {
                case NavMeshPath path:
                    jsonObject[p.Name] = ObjectStateJSON(path);
                    break;
                default:
                    jsonObject[p.Name] = result.ToString();
                    break;
            }
        }

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var f in fields)
        {
            object result = null;

            try
            {
                if (skipFields.Contains(f.Name)) continue;

                result = f.GetValue(obj);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = e.InnerException?.Message ?? e.Message;
            }

            switch (result)
            {
                case NavMeshPath path:
                    jsonObject[f.Name] = ObjectStateJSON(path);
                    break;
                default:
                    jsonObject[f.Name] = result.ToString();
                    break;
            }
        }

        return jsonObject;
    }

    public static string DumpState(this Object obj, bool prettyPrint = true)
    {
        var jsonObject = ObjectStateJSON(obj);

        return jsonObject.ToString(prettyPrint ? 2 : 0);
    }
}
