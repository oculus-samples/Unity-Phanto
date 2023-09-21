// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace PhantoUtils
{
    public class Selector : MonoBehaviour
    {
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private TMP_Text text;

        [SerializeField] private List<string> options = new() { "Option 1", "Option 2", "Option 3" };

        [SerializeField] private int selectedIndex;

        public List<string> Options
        {
            get => options;
            set
            {
                options = value;
                SelectedIndex = selectedIndex;
                UpdateSelection();
            }
        }

        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                var newValue = (value % options.Count + options.Count) % options.Count;
                if (selectedIndex == newValue) return;
                selectedIndex = newValue;
                UpdateSelection();
            }
        }

        private void Awake()
        {
            Assert.IsNotNull(leftButton, $"{nameof(leftButton)} cannot be null.");
            Assert.IsNotNull(rightButton, $"{nameof(rightButton)} cannot be null.");
            Assert.IsNotNull(text, $"{nameof(text)} cannot be null.");

            leftButton.onClick.AddListener(() => { SelectedIndex -= 1; });

            rightButton.onClick.AddListener(() => { SelectedIndex += 1; });
        }

        public event Action<int> selectionChanged;

        private void UpdateSelection()
        {
            var selection = options[selectedIndex];
            text.text = selection;
            selectionChanged?.Invoke(selectedIndex);
        }
    }
}
