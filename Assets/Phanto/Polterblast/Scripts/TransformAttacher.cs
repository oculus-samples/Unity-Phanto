// Copyright (c) Meta Platforms, Inc. and affiliates.

// @formatter:on

using UnityEngine;

/// <summary>
/// Attaches transform to parent
/// </summary>
public class TransformAttacher : MonoBehaviour
{
    public string parentObjectName;
    private GameObject newParent;

    // Start is called before the first frame update
    private void Start()
    {
        newParent = GameObject.Find(parentObjectName);
        if (newParent != null)
            transform.parent = newParent.transform;
    }

    // Update is called once per frame
    private void Update()
    {
    }

    public void AttachToParent()
    {
        if (newParent == null) newParent = GameObject.Find(parentObjectName);

        if (newParent != null) transform.parent = newParent.transform;
    }
}
