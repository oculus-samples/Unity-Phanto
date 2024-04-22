// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Crystal : MonoBehaviour
{
    private Transform _transform;

    private Vector3 _defaultScale;

    public Vector3 Position => _transform.position;

    protected virtual void Awake()
    {
        _transform = transform;
        _defaultScale = _transform.localScale;
    }

    public void LookAtPoint(Vector3 point, Vector3 upAxis)
    {
        _transform.LookAt(point, upAxis);
    }

    public void RandomizeRotation(Vector3 upAxis)
    {
        _transform.rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), upAxis);
    }

    public void RandomizeScale(float min, float max)
    {
        _transform.localScale *= Random.Range(min, max);
    }

    public void Destruct()
    {
        Destroy(gameObject);
    }

    public void Show(bool visible = true)
    {
        gameObject.SetActive(visible);
    }

    public void Hide()
    {
        Show(false);
    }
}
