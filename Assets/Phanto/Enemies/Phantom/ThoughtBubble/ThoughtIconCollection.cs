// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Phantom
{
    public enum Thought
    {
        None,
        Couch,
        Door,
        Window,
        Other,
        Storage,
        Bed,
        Screen,
        Lamp,
        Plant,
        Table,
        WallArt,
        Question,
        Exclamation,
        Ghost,
        Surprise,
        Alert,
        Angry,
    }

    [CreateAssetMenu]
    public class ThoughtIconCollection : ScriptableObject
    {
        [Serializable]
        private class ThoughtIcon
        {
            [HideInInspector] public string name;
            public Thought thought;
            public Sprite sprite;

            internal void OnValidate()
            {
                name = thought.ToString();
            }
        }

        [SerializeField] private List<ThoughtIcon> thoughtIcons;

        private readonly Dictionary<Thought, Sprite> iconDictionary = new Dictionary<Thought, Sprite>();

        private void OnEnable()
        {
            PopulateDictionary();
        }

        private void PopulateDictionary()
        {
            iconDictionary.Clear();
            foreach (var item in thoughtIcons)
            {
                if (!iconDictionary.TryAdd(item.thought, item.sprite))
                {
                    Debug.LogWarning($"Duplicate icon for thought: {item.thought}");
                }
            }
        }

        public bool TryGetIcon(Thought thought, out Sprite sprite)
        {
            return iconDictionary.TryGetValue(thought, out sprite);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (var thought in thoughtIcons)
            {
                thought.OnValidate();
            }
        }
#endif
    }
}
