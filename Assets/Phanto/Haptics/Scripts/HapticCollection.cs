// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Haptics;
using PhantoUtils;
using UnityEngine;

[CreateAssetMenu(menuName = "Phanto/Haptics/Haptics Collection")]
public class HapticCollection : ScriptableObject
{
    [Serializable]
    public class HapticEntry
    {
        public string name;
        public HapticClip hapticClip;

        internal void OnValidate()
        {
            if (string.IsNullOrEmpty(name) && hapticClip != null)
            {
                name = hapticClip.name;
            }
        }
    }

    [SerializeField] private List<HapticEntry> hapticsList;

    private readonly Dictionary<string, HapticClip> _hapticDictionary = new Dictionary<string, HapticClip>();

    private readonly Dictionary<HapticClip, HapticClipPlayer> _hapticPlayerDictionary =
        new Dictionary<HapticClip, HapticClipPlayer>();

    private void OnEnable()
    {
        InitializeDictionary();
    }

    private void OnDisable()
    {
        foreach (var value in _hapticPlayerDictionary.Values)
        {
            value?.Dispose();
        }

        _hapticPlayerDictionary.Clear();
    }

    private void InitializeDictionary()
    {
        _hapticDictionary.Clear();

        foreach (var entry in hapticsList)
        {
            _hapticDictionary.TryAdd(entry.name, entry.hapticClip);
        }
    }

    public HapticClip GetRandomClip()
    {
        var entry = hapticsList.RandomElement();

        return entry.hapticClip;
    }

    public HapticClipPlayer GetRandomPlayer()
    {
        var clip = GetRandomClip();

        TryGetPlayer(clip, out var player);

        return player;
    }

    public bool TryGetClip(string key, out HapticClip hapticClip)
    {
        if (_hapticDictionary.Count == 0)
        {
            InitializeDictionary();
        }

        return _hapticDictionary.TryGetValue(key, out hapticClip);
    }

    public bool TryGetPlayer(HapticClip clip, out HapticClipPlayer hapticPlayer)
    {
        if (!_hapticPlayerDictionary.TryGetValue(clip, out hapticPlayer))
        {
            hapticPlayer = new HapticClipPlayer(clip);
            _hapticPlayerDictionary.Add(clip, hapticPlayer);
        }

        return hapticPlayer != null;
    }

    public bool TryGetPlayer(string key, out HapticClipPlayer hapticPlayer)
    {
        if (!TryGetClip(key, out var clip))
        {
            hapticPlayer = null;
            return false;
        }

        return TryGetPlayer(clip, out hapticPlayer);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var dupeNames = new HashSet<string>(hapticsList.Count);
        foreach (var entry in hapticsList)
        {
            entry.OnValidate();

            if (!string.IsNullOrEmpty(entry.name) && !dupeNames.Add(entry.name))
            {
                Debug.LogWarning($"Duplicate haptic clip name: '{entry.name}'. Get by key will only return first clip.");
            }
        }
    }
#endif
}
