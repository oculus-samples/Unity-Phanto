// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Phantom;
using PhantoUtils;
using UnityEngine;

public class BouncingPhantomController : MonoBehaviour
{
    private readonly WaitForSeconds _lifeSpan = new(5.0f);

    private Coroutine _lifeCoroutine;
    private CollisionDemoManager _manager;
    private Rigidbody _rigidbody;

    private Transform _transform;

    private void Awake()
    {
        _transform = transform;
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision other)
    {
        _manager.SpawnGoo(other);
    }

    public void Initialize(CollisionDemoManager collisionDemoManager)
    {
        _manager = collisionDemoManager;
    }

    public void Show(bool visible = true)
    {
        gameObject.SetActive(visible);
    }

    public void Hide()
    {
        Show(false);
    }

    public void Launch(Ray ray)
    {
        _transform.SetPositionAndRotation(ray.origin, Quaternion.LookRotation(ray.direction));

        Show();

        _rigidbody.LaunchProjectile(ray.origin, _transform.forward * Random.Range(7.0f, 10.0f));

        if (_lifeCoroutine != null) StopCoroutine(_lifeCoroutine);

        _lifeCoroutine = StartCoroutine(LifeSpan());
    }

    private IEnumerator LifeSpan()
    {
        yield return _lifeSpan;

        Hide();

        _lifeCoroutine = null;
    }
}
