// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Interaction;

namespace PhantoUtils.VR
{
    public static class InteractionExtensions
    {
        public static RayInteractable GetHoveredInteractable(this RayInteractor interactor)
        {
            if (interactor.State != InteractorState.Hover) return null;
            return interactor.Interactable;
        }

        public static bool IsInteractorHovering(this RayInteractable interactable, RayInteractor interactor)
        {
            if (!(interactor.State == InteractorState.Hover || interactor.State == InteractorState.Select))
                return false;
            return interactor.Interactable == interactable;
        }
    }
}
