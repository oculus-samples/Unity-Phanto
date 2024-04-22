// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Oculus.Interaction;
using Oculus.Interaction.Input.Visuals;
using UnityEngine;

namespace PhantoUtils.VR
{
    [DefaultExecutionOrder(-10)]
    public sealed class InteractionRig : MonoBehaviour
    {
        public enum InteractorType
        {
            None,
            LeftControllerInteractor,
            LeftHandInteractor,
            LeftInteractor,
            RightControllerInteractor,
            RightHandInteractor,
            RightInteractor
        }

        [SerializeField] private Transform leftControllerRoot;
        [SerializeField] private Transform rightControllerRoot;
        [SerializeField] private Transform leftHandRoot;
        [SerializeField] private Transform rightHandRoot;

        [SerializeField] private OVRControllerVisual leftControllerVisual;
        [SerializeField] private OVRControllerVisual rightControllerVisual;

        [SerializeField] private RayInteractor leftControllerRayInteractor;
        [SerializeField] private RayInteractor leftHandRayInteractor;
        [SerializeField] private RayInteractor rightControllerRayInteractor;
        [SerializeField] private RayInteractor rightHandRayInteractor;

        public InteractorType DominantInteractorType { get; private set; }

        public InteractorType NonDominantInteractorType => DominantInteractorType == InteractorType.RightInteractor
            ? InteractorType.LeftInteractor
            : InteractorType.RightInteractor;

        public RayInteractor DominantRayInteractor => GetRayInteractor(DominantInteractorType);
        public RayInteractor NonDominantRayInteractor => GetRayInteractor(NonDominantInteractorType);
        public RayInteractor ActiveRayInteractor => GetRayInteractor(ActiveInteractorType);

        public InteractorType ActiveInteractorType
        {
            get
            {
                switch (OVRInput.GetActiveController())
                {
                    case OVRInput.Controller.LTouch:
                        return InteractorType.LeftControllerInteractor;
                    case OVRInput.Controller.RTouch:
                        return InteractorType.RightControllerInteractor;
                    case OVRInput.Controller.LHand:
                        return InteractorType.LeftHandInteractor;
                    case OVRInput.Controller.RHand:
                        return InteractorType.RightHandInteractor;
                }

                return InteractorType.None;
            }
        }

        private void Awake()
        {
            FindDependencies();
        }

        private void Start()
        {
            DominantInteractorType = OVRInput.GetDominantHand() == OVRInput.Handedness.LeftHanded
                ? InteractorType.LeftInteractor
                : InteractorType.RightInteractor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            FindDependencies();
        }
#endif

        public RayInteractor GetRayInteractor(InteractorType interactorType)
        {
            switch (interactorType)
            {
                case InteractorType.LeftControllerInteractor:
                    return leftControllerRayInteractor;
                case InteractorType.LeftHandInteractor:
                    return leftHandRayInteractor;
                case InteractorType.LeftInteractor:
                    return leftControllerRayInteractor; // TODO determine which is active
                case InteractorType.RightControllerInteractor:
                    return rightControllerRayInteractor;
                case InteractorType.RightHandInteractor:
                    return rightHandRayInteractor;
                case InteractorType.RightInteractor:
                    return rightControllerRayInteractor;
                default:
                    throw new ArgumentOutOfRangeException(nameof(interactorType), interactorType, null);
            }
        }

        public void SetControllerVisualsVisible(bool isVisible)
        {
            if (leftControllerVisual != null) leftControllerVisual.gameObject.SetActive(isVisible);
            if (rightControllerVisual != null) rightControllerVisual.gameObject.SetActive(isVisible);
        }

        public void SetRayInteractorsEnabled(bool isEnabled)
        {
            if (leftControllerRayInteractor != null) leftControllerRayInteractor.gameObject.SetActive(isEnabled);
            if (rightControllerRayInteractor != null) rightControllerRayInteractor.gameObject.SetActive(isEnabled);
            if (leftHandRayInteractor != null) leftHandRayInteractor.gameObject.SetActive(isEnabled);
            if (rightHandRayInteractor != null) rightHandRayInteractor.gameObject.SetActive(isEnabled);
        }

        private void FindDependencies()
        {
            if (leftControllerRoot != null)
            {
                if (leftControllerRayInteractor == null)
                    leftControllerRayInteractor = leftControllerRoot.GetComponentInChildren<RayInteractor>();
                if (leftControllerVisual == null)
                    leftControllerVisual = leftControllerRoot.GetComponentInChildren<OVRControllerVisual>();
            }

            if (rightControllerRoot != null)
            {
                if (rightControllerRayInteractor == null)
                    rightControllerRayInteractor = rightControllerRoot.GetComponentInChildren<RayInteractor>();
                if (rightControllerVisual == null)
                    rightControllerVisual = rightControllerRoot.GetComponentInChildren<OVRControllerVisual>();
            }

            if (leftHandRoot != null)
                if (leftHandRayInteractor == null)
                    leftHandRayInteractor = leftHandRoot.GetComponentInChildren<RayInteractor>();
            if (rightHandRoot != null)
                if (rightHandRayInteractor == null)
                    rightHandRayInteractor = rightHandRoot.GetComponentInChildren<RayInteractor>();
        }
    }
}
