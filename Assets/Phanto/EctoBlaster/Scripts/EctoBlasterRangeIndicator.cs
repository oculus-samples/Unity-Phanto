// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Phantom.EctoBlaster.Scripts
{
    /// <summary>
    ///     This script controls the blaster range indicator size and his visibility based on app input focus status
    /// </summary>
    public class EctoBlasterRangeIndicator : MonoBehaviour
    {
        public void SetBlasterRangeIndicator(Vector3 size)
        {
            transform.localScale = size;
        }

        [SerializeField] private GameObject visualModel;

        private void Awake()
        {
            OVRManager.InputFocusAcquired += OnFocusAcquired;
            OVRManager.InputFocusLost += OnFocusLost;
        }

        private void OnDestroy()
        {
            OVRManager.InputFocusAcquired -= OnFocusAcquired;
            OVRManager.InputFocusLost -= OnFocusLost;
        }

        private void OnFocusLost()
        {
            visualModel.SetActive(false);
        }

        private void OnFocusAcquired()
        {
            visualModel.SetActive(true);
        }
    }



}
