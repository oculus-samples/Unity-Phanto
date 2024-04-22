// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace PhantoUtils.VR
{
    [SingletonMonoBehaviour.InstantiationSettings(dontDestroyOnLoad = true)]
    [DefaultExecutionOrder(-10)]
    public sealed class CameraRig : SingletonMonoBehaviour<CameraRig>
    {
        [SerializeField] private OVRCameraRig cameraRig;

        [SerializeField] private InteractionRig interactionRig;

        public OVRCameraRig OVRCameraRig => cameraRig;
        public Transform CenterEyeAnchor => cameraRig.centerEyeAnchor;
        public Transform LeftHandAnchor => cameraRig.leftHandAnchor;
        public Transform RightHandAnchor => cameraRig.rightHandAnchor;
        public Transform LeftControllerAnchor => cameraRig.leftControllerAnchor;
        public Transform RightControllerAnchor => cameraRig.rightControllerAnchor;

        public InteractionRig InteractionRig => interactionRig;

        protected override void Awake()
        {
            base.Awake();
            FindDependencies();

            if (cameraRig == null)
            {
                Debug.LogError($"{nameof(CameraRig)}: could not find a reference to OVRCameraRig.");
                Destroy(this);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            FindDependencies();
        }
#endif

        private void FindDependencies()
        {
            if (cameraRig == null)
            {
                cameraRig = GetComponentInChildren<OVRCameraRig>();
            }

            if (interactionRig == null)
            {
                interactionRig = GetComponent<InteractionRig>();
            }
        }
    }
}
