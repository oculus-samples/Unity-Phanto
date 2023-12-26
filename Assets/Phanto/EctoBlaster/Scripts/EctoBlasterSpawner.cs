// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Oculus.Haptics;
using Phanto.Audio.Scripts;
using UnityEditor;
using UnityEngine;

namespace Phantom.EctoBlaster.Scripts
{
    /// <summary>
    /// Spawns blaster on click
    /// </summary>
    public class EctoBlasterSpawner : MonoBehaviour
    {
        [Tooltip("A reference to the blaster prefab")]
        [SerializeField] private GameObject blasterPrefab;

        [Tooltip("A reference to the blaster preview prefab")]
        [SerializeField] private GameObject blasterPreviewPrefab;

        [Tooltip("A reference to the blaster range indicator prefab")]
        [SerializeField] private GameObject blasterRangeIndicatorPrefab;

        [Tooltip("The layer mask used to detect which objects can the blaster be spawned on")]
        [SerializeField] private LayerMask meshLayerMask;

        [Tooltip("The layer mask used to detect which objects will be targeted by the blaster")]
        [SerializeField] private LayerMask blasterLayerMask;

        [Tooltip("The button used to spawn the blaster")]
        [SerializeField] private OVRInput.RawButton spawnButton;

        [Tooltip("The radius to start blasting the target")] [SerializeField]
        private float blastRadius = 0.5f;

        [Tooltip("The effective size of the blaster radius")]
        [SerializeField] private float visualBlastRadius = 2.0f;

        [Tooltip("The sound effects played when the blaster is spawned")]
        [SerializeField] private PhantoRandomOneshotSfxBehavior placeDownSFX;

        [Tooltip("The sound effects played when the blaster is picked up")]
        [SerializeField] private PhantoRandomOneshotSfxBehavior pickUpSFX;

        [SerializeField] private HapticClip placeDownHaptic;
        [SerializeField] private HapticClip pickUpHaptic;

        [SerializeField] private Controller placementController = Controller.Left;

        [SerializeField, Range(0.1f, 10.0f)] private float hapticClipPlayerAmplitude = 1.0f;

        // Private fields
        private GameObject _blaster;
        private GameObject _blasterPreview;
        private GameObject _blasterRangeIndicator;
        private bool _hasSpawned; // Flag to check if the object has been spawned already
        private EctoBlasterRangeIndicator _rangeIndicator; // Range indicator

        private HapticClipPlayer _placeDownHapticPlayer;
        private HapticClipPlayer _pickUpHapticPlayer;

        private void Start()
        {
            _blasterPreview = Instantiate(blasterPreviewPrefab, transform);
            _blasterRangeIndicator = Instantiate(blasterRangeIndicatorPrefab, transform);
            _rangeIndicator = _blasterRangeIndicator.GetComponentInChildren<EctoBlasterRangeIndicator>();

            _placeDownHapticPlayer = new HapticClipPlayer(placeDownHaptic);
            _placeDownHapticPlayer.amplitude = hapticClipPlayerAmplitude;

            _pickUpHapticPlayer = new HapticClipPlayer(pickUpHaptic);
            _pickUpHapticPlayer.amplitude = hapticClipPlayerAmplitude;
        }

        private void OnDestroy()
        {
            _placeDownHapticPlayer?.Dispose();
            _pickUpHapticPlayer?.Dispose();
        }

        private void Update()
        {
            // Perform the raycast from the controller
            var ray = new Ray(transform.position, transform.forward);

            if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, meshLayerMask | blasterLayerMask))
                return;

            if (_hasSpawned && OVRInput.GetDown(spawnButton))
            {
                Destroy(_blaster);
                _blasterPreview.SetActive(true);
                _blasterRangeIndicator.SetActive(true);
                _hasSpawned = false;

                pickUpSFX.PlaySfxAtPosition(hit.point);
                _pickUpHapticPlayer.Play(placementController);
            }
            else
            {
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, meshLayerMask))
                {
                    // Check if the hit object is the one you want to spawn
                    if (!_hasSpawned && OVRInput.GetDown(spawnButton))
                    {
                        // Spawn the game object
                        _blaster = Instantiate(blasterPrefab, hit.point, Quaternion.identity);
                        _blaster.transform.up = hit.normal;
                        var blasterRadar = _blaster.GetComponent<EctoBlasterRadar>();
                        blasterRadar.BlastRadius = blastRadius;

                        _blasterPreview.SetActive(false);
                        _blasterRangeIndicator.SetActive(false);
                        _hasSpawned = true;

                        placeDownSFX.PlaySfxAtPosition(hit.point);
                        _placeDownHapticPlayer.Play(placementController);
                    }
                    else
                    {
                        // Place the range indicator
                        _rangeIndicator.SetBlasterRangeIndicator(Vector3.one * blastRadius * visualBlastRadius);
                        _blasterRangeIndicator.transform.position = hit.point;
                        _blasterRangeIndicator.transform.up = hit.normal;
                        _blasterPreview.transform.position = hit.point;
                        _blasterPreview.transform.up = hit.normal;
                    }
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (_placeDownHapticPlayer != null)
            {
                _placeDownHapticPlayer.amplitude = hapticClipPlayerAmplitude;
            }

            if (_pickUpHapticPlayer != null)
            {
                _pickUpHapticPlayer.amplitude = hapticClipPlayerAmplitude;
            }
        }
#endif
    }
}
