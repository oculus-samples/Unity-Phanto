// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
///     This script resets the transform back to where it started.
/// </summary>
public class TransformReset : MonoBehaviour
{
    public float returnHomeTime = 1.5f;
    public GameObject returnHomeObject;

    protected Grabbable _grabbable;
    private bool grabbed;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
    }

    private void Update()
    {
        if (!grabbed && Vector3.Distance(transform.position, returnHomeObject.transform.position) >= 0.02f)
            StartCoroutine(ReturnHome());
    }

    public void Grabbed()
    {
        if (!grabbed) grabbed = true;
    }

    public void Released()
    {
        grabbed = false;
        StartCoroutine(ReturnHome());
    }

    /// <summary>
    ///     Resets the transform back to where it started.
    /// </summary>
    private IEnumerator ReturnHome()
    {
        float timer = 0;
        while (timer < returnHomeTime)
        {
            if (grabbed) timer = returnHomeTime;

            timer += Time.deltaTime;
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, returnHomeObject.transform.position,
                timer / returnHomeTime), Quaternion.Lerp(transform.rotation, returnHomeObject.transform.rotation,
                timer / returnHomeTime));
            yield return null;
        }

        if (!grabbed)
            transform.SetPositionAndRotation(returnHomeObject.transform.position, returnHomeObject.transform.rotation);
    }
}
