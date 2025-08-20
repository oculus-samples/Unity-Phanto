// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using UnityEngine;

public class DemoGoo : MonoBehaviour
{
    [SerializeField] private GooController gooController;

    private readonly WaitForSeconds _bubbleDuration = new(0.2f);
    private Transform _transform;

    private void Awake()
    {
        _transform = transform;
    }

    private void OnEnable()
    {
        gooController.Stopped += OnParticlesStopped;
    }

    private void OnDisable()
    {
        gooController.Stopped -= OnParticlesStopped;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gooController == null) gooController = GetComponent<GooController>();
    }
#endif

    public void Show(bool visible = true)
    {
        gameObject.SetActive(visible);
    }

    public void Hide()
    {
        Show(false);
    }

    private void OnParticlesStopped()
    {
        gameObject.SetActive(false);
    }

    public void SpawnAt(Vector3 position, Vector3 normal)
    {
        _transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal));

        Show();
        StartCoroutine(Bubble());
    }

    private IEnumerator Bubble()
    {
        yield return _bubbleDuration;
        gooController.Extinguish(null);
    }
}
