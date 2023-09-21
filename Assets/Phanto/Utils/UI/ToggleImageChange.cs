// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace PhantoUtils
{
    public class ToggleImageChange : MonoBehaviour
    {
        [SerializeField] private Image _targetImage;

        [SerializeField] private Toggle _targetToggle;

        [SerializeField] private Sprite _toggledImage;

        [SerializeField] private Sprite _untoggledImage;

        public Sprite ToggledImage
        {
            get => _toggledImage;
            set
            {
                _toggledImage = value;
                UpdateImage();
            }
        }

        public Sprite UntoggledImage
        {
            get => _untoggledImage;
            set
            {
                _untoggledImage = value;
                UpdateImage();
            }
        }

        private void Awake()
        {
            FindDependencies();
        }

        private void Start()
        {
            Assert.IsNotNull(_targetImage, $"{nameof(_targetImage)} cannot be null.");
            Assert.IsNotNull(_targetToggle, $"{nameof(_targetToggle)} cannot be null.");
        }

        private void OnEnable()
        {
            _targetToggle.onValueChanged.AddListener(OnValueChanged);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            FindDependencies();
        }
#endif

        private void UpdateImage()
        {
            if (_targetImage == null || _targetToggle == null) return;
            _targetImage.sprite = _targetToggle.isOn ? _toggledImage : _untoggledImage;
        }

        private void OnValueChanged(bool value)
        {
            UpdateImage();
        }

        private void FindDependencies()
        {
            if (_targetImage == null) _targetImage = GetComponentInChildren<Image>();
            if (_targetToggle == null) _targetToggle = GetComponentInChildren<Toggle>();
        }
    }
}
