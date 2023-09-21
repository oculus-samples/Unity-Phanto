// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace PhantoUtils.VR
{
    public class AutoSizeRaycastHitbox : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;

        [SerializeField] private BoxCollider hitbox;

        [SerializeField] private bool resizeOnUpdate;

        public RectTransform Panel
        {
            get => panel;
            set
            {
                panel = value;
                ResizeHitbox();
            }
        }

        public BoxCollider Hitbox
        {
            get => hitbox;
            set
            {
                hitbox = value;
                ResizeHitbox();
            }
        }

        private void Update()
        {
            if (resizeOnUpdate) ResizeHitbox();
        }

        private void OnEnable()
        {
            ResizeHitbox();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (panel == null) panel = GetComponent<RectTransform>();
            if (hitbox == null) hitbox = GetComponent<BoxCollider>();
        }
#endif

        private void ResizeHitbox()
        {
            if (panel == null || hitbox == null) return;

            hitbox.size = new Vector3(panel.rect.width, panel.rect.height, 0.01f);
        }
    }
}
