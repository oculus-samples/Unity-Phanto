// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Haptics;

[MetaCodeSample("Phanto")]
public class HapticsPhantoTouch : MonoBehaviour
{
    private static readonly Dictionary<Collider, HapticsPhantoTouch> _phantoTouches =
        new Dictionary<Collider, HapticsPhantoTouch>();

    [SerializeField] private Controller hapticController = Controller.Right;

    private Collider _collider;

    public Controller Controller => hapticController;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        _phantoTouches.TryAdd(_collider, this);
    }

    private void OnDisable()
    {
        _phantoTouches.Remove(_collider);
    }

    public static bool TryGetPhantoTouch(Collider collider, out HapticsPhantoTouch phantoTouch)
    {
        return _phantoTouches.TryGetValue(collider, out phantoTouch);
    }
}
