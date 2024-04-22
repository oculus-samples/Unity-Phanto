// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Oculus.Haptics;
using Phanto.Audio.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace Phantom.EctoBlaster.Scripts
{
    /// <summary>
    /// Spawns blaster on click
    /// </summary>
    public class EctoBlasterSpawner : MonoBehaviour
    {
        [Tooltip("A reference to the blaster prefab")] [SerializeField]
        private GameObject blasterPrefab;

        [Tooltip("A reference to the blaster preview prefab")] [SerializeField]
        private GameObject blasterPreviewPrefab;

        [Tooltip("A reference to the blaster range indicator prefab")] [SerializeField]
        private GameObject blasterRangeIndicatorPrefab;

        [Tooltip("The layer mask used to detect which objects can the blaster be spawned on")] [SerializeField]
        private LayerMask meshLayerMask;

        [Tooltip("The layer mask used to detect which objects will be targeted by the blaster")] [SerializeField]
        private LayerMask blasterLayerMask;

        [Tooltip("The button used to spawn the blaster")] [SerializeField]
        private OVRInput.RawButton spawnButton;

        [Tooltip("The radius to start blasting the target")] [SerializeField]
        private float blastRadius = 0.5f;

        [Tooltip("The effective size of the blaster radius")] [SerializeField]
        private float visualBlastRadius = 2.0f;

        [Tooltip("The sound effects played when the blaster is spawned")] [SerializeField]
        private PhantoRandomOneShotSfxBehavior placeDownSFX;

        [Tooltip("The sound effects played when the blaster is picked up")] [SerializeField]
        private PhantoRandomOneShotSfxBehavior pickUpSFX;

        [Tooltip("Blaster Respawn Time")] [SerializeField]
        private float respawnTime = 5f;

        [Tooltip("Blaster destroy Time")] [SerializeField]
        private float blasterDestroyTime = 8f;


        [SerializeField] private HapticClip placeDownHaptic;
        [SerializeField] private HapticClip pickUpHaptic;

        [SerializeField] private Controller placementController = Controller.Left;

        [SerializeField, Range(0.1f, 10.0f)] private float hapticClipPlayerAmplitude = 1.0f;

        [SerializeField]
        private EctoBlasterTrajectoryLine trajectoryLine;

        // Public fields
        public UnityEvent<RaycastHit> onBlasterPreview;
        public UnityEvent<RaycastHit> onBlasterPlaced;

        // Private fields
        private GameObject _blaster;
        private GameObject _blasterPreview;
        private GameObject _blasterRangeIndicator;
        private bool _hasSpawned; // Flag to check if the object has been spawned already
        private EctoBlasterRangeIndicator _rangeIndicator; // Range indicator

        private HapticClipPlayer _placeDownHapticPlayer;
        private HapticClipPlayer _pickUpHapticPlayer;
        private bool _isVisible;

        IEnumerator Start()
        {
            _blasterPreview = Instantiate(blasterPreviewPrefab, transform);
            _blasterPreview.SetActive(true);
            _blasterRangeIndicator = Instantiate(blasterRangeIndicatorPrefab, transform);
            _blasterRangeIndicator.SetActive(true);
            _rangeIndicator = _blasterRangeIndicator.GetComponentInChildren<EctoBlasterRangeIndicator>();

            _placeDownHapticPlayer = new HapticClipPlayer(placeDownHaptic);
            _placeDownHapticPlayer.amplitude = hapticClipPlayerAmplitude;

            _pickUpHapticPlayer = new HapticClipPlayer(pickUpHaptic);
            _pickUpHapticPlayer.amplitude = hapticClipPlayerAmplitude;

            if (GameplaySettingsManager.Instance.WavesAvailable)
            {
                // Blaster visibility is dependant on the game current wave
                GameplaySettingsManager.Instance.OnNewWave.AddListener(OnNewWave);
                ToggleVisibility(false);
            }
            else
            {
                ToggleVisibility(true); // Show blaster when not in the game
            }

            while (true)
            {
                yield return new WaitUntil(() => _hasSpawned);
                yield return new WaitForSeconds(respawnTime);

                var ray = new Ray(transform.position, transform.forward);

                if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, meshLayerMask | blasterLayerMask))
                    yield return null;

                if (_hasSpawned && _isVisible)
                {
                    DetachBlaster(hit);
                }
            }
        }

        private void OnNewWave(GameplaySettings.WaveSettings newWaveSettings)
        {
            var isOnCorrectHand = GetCorrectHand();
            ToggleVisibility(newWaveSettings.phantomSetting.isEnabled && isOnCorrectHand);
        }

        /// <summary>
        /// Ask whether the current wave settings apply for the current hand
        /// </summary>
        /// <returns></returns>
        private bool GetCorrectHand()
        {
            if (TryGetComponent<SoftParent>(out var parent))
            {
                if (GameplaySettingsManager.Instance.gameplaySettings.CurrentPhantomSetting.blasterHands ==
                    GameplaySettings.PhantomSetting.BlasterHands.Both)
                {
                    return true;
                }

                switch (parent.ParentTarget)
                {
                    case SoftParent.ParentTargetType.LeftController:
                    case SoftParent.ParentTargetType.LeftHand:
                    {
                        return GameplaySettingsManager.Instance.gameplaySettings.CurrentPhantomSetting.blasterHands ==
                               GameplaySettings.PhantomSetting.BlasterHands.LeftHand;
                    }
                    case SoftParent.ParentTargetType.RightController:
                    case SoftParent.ParentTargetType.RightHand:
                    {
                        return GameplaySettingsManager.Instance.gameplaySettings.CurrentPhantomSetting.blasterHands ==
                               GameplaySettings.PhantomSetting.BlasterHands.RightHand;
                    }
                    default:
                        return true;
                }
            }

            // If we don't have a soft parent component, we ignore this condition
            return true;
        }

        private void OnDestroy()
        {
            _placeDownHapticPlayer?.Dispose();
            _pickUpHapticPlayer?.Dispose();
        }

        private void Update()
        {
            if (_isVisible)
            {
                // Perform the raycast from the controller
                var ray = new Ray(transform.position, transform.forward);

                if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, meshLayerMask | blasterLayerMask))
                    return;

                if (Physics.Raycast(ray, out hit, Mathf.Infinity, meshLayerMask))
                {
                    // Check if the hit object is the one you want to spawn
                    if (!_hasSpawned && OVRInput.GetDown(spawnButton))
                    {
                        AttachBlaster(hit);
                    }
                    else
                    {
                        ShowRangeIndicator(hit);
                    }
                }
            }
        }

        private void ShowRangeIndicator(RaycastHit hit)
        {
            if (!_hasSpawned)
                onBlasterPreview?.Invoke(hit);

            // Place the range indicator
            _rangeIndicator.SetBlasterRangeIndicator(Vector3.one * blastRadius * visualBlastRadius);
            _blasterRangeIndicator.transform.position = hit.point;
            _blasterRangeIndicator.transform.up = hit.normal;
            if (_blasterPreview != null)
            {
                _blasterPreview.transform.position = hit.point;
                _blasterPreview.transform.up = hit.normal;
            }
        }

        private void AttachBlaster(RaycastHit hit)
        {
            onBlasterPlaced?.Invoke(hit);
            Destroy(_blasterPreview);
            // Spawn the game object
            _blaster = Instantiate(blasterPrefab, hit.point, Quaternion.identity);
            _blaster.SetActive(true);
            _blaster.transform.up = hit.normal;
            var blasterRadar = _blaster.GetComponent<EctoBlasterRadar>();
            blasterRadar.BlastRadius = blastRadius;
            blasterRadar.DestroyTime = blasterDestroyTime;

            _blasterPreview.SetActive(false);
            _blasterRangeIndicator.SetActive(false);
            _hasSpawned = true;


            placeDownSFX.PlaySfxAtPosition(hit.point);
            _placeDownHapticPlayer.Play(placementController);
        }


        private void DetachBlaster(RaycastHit hit)
        {
            if (_blasterPreview == null)
            {
                _blasterPreview = Instantiate(blasterPreviewPrefab, transform);
                _blaster.SetActive(true);
            }

            _blasterPreview.SetActive(true);
            _blasterRangeIndicator.SetActive(true);
            _hasSpawned = false;

            pickUpSFX.PlaySfxAtPosition(hit.point);
            _pickUpHapticPlayer.Play(placementController);
        }

        private void ToggleVisibility(bool visible)
        {
            _isVisible = visible;
            if (!_isVisible)
            {
                if (_blaster != null)
                    _blaster.SetActive(false);
                if (_blasterPreview != null)
                    _blasterPreview.SetActive(false);
                if (_blasterRangeIndicator != null)
                    _blasterRangeIndicator.SetActive(false);
                trajectoryLine.Hide();
            }
            else
            {
                if (_blasterPreview == null)
                {
                    _blasterPreview = Instantiate(blasterPreviewPrefab, transform);
                }

                _blasterPreview.SetActive(true);
                _blasterRangeIndicator.SetActive(true);
                trajectoryLine.Show();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (trajectoryLine == null)
            {
                trajectoryLine = GetComponent<EctoBlasterTrajectoryLine>();
            }

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
