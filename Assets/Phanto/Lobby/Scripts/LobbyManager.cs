// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Haptics;
using Phantom.Environment.Scripts;
using PhantoUtils;
using PhantoUtils.VR;
using UnityEngine;
using UnityEngine.Android;

/// <summary>
/// Manages the state of the Lobby UI
/// </summary>
public class LobbyManager : MonoBehaviour
{
    private const string WINDOW_CLOSE_CLIP = "WindowClose";

    // Permission string for requesting scene data permission
    private static readonly string SCENE_PERMISSION = "com.oculus.permission.USE_SCENE";

    // references to scene manager prefabs and other UI game objects
    [SerializeField] private GameObject SceneApiDataLoaderReference;
    [SerializeField] private GameObject GameSceneLoaderReference;
    [SerializeField] private GameObject InstructionsPrefab;
    [SerializeField] private GameObject PermissionParent;
    [SerializeField] private GameObject PermissionDontAskAgain;
    [SerializeField] private GameObject NoSceneModelParent;

    // Assigned buttons to operate the menus
    [SerializeField] private OVRInput.RawButton StartGameButton;
    [SerializeField] private OVRInput.RawButton RescanButton;

    [SerializeField] private HapticCollection uiHaptics;

    private SceneDataLoader _dataLoader;
    private SceneLoader _sceneLoader;

    private bool _permissionGranted = false;
    private bool _boundsSet = false;

    private void Awake()
    {
        ScenePermissionGrantedBroadcaster.PermissionGrantedEvent += ActOnPermissionGranted;

        if (ApplicationUtils.IsVR())
        {
            SceneApiDataLoaderReference.SetActive(false); // Turn off scene related stuff until permission is granted
            RequestScenePermission();
        }
        else
            ActOnPermissionGranted();
    }

    private void OnEnable()
    {
        SceneBoundsChecker.BoundsChanged += OnBoundsChanged;
    }

    private void OnDisable()
    {
        SceneBoundsChecker.BoundsChanged -= OnBoundsChanged;
    }

    private void OnDestroy()
    {
        ScenePermissionGrantedBroadcaster.PermissionGrantedEvent -= ActOnPermissionGranted;
    }

    private void Update()
    {
        // Handle cases when the user does not granted scene data permission
        if (PermissionParent.activeSelf)
        {
            if (OVRInput.GetDown(StartGameButton) || Input.GetKeyDown(KeyCode.Space))
            {
                PlayHaptic(WINDOW_CLOSE_CLIP, StartGameButton);
                RequestScenePermission();
            }
        }

        // Handle cases when the user does not have a space setup
        if (NoSceneModelParent.activeSelf)
        {
            if (_dataLoader == null) _dataLoader = SceneApiDataLoaderReference.GetComponent<SceneDataLoader>();
            if (_sceneLoader == null) _sceneLoader = GameSceneLoaderReference.GetComponent<SceneLoader>();

            // Handle notification where no scene model is present
            if (OVRInput.GetDown(RescanButton) || Input.GetKeyDown(KeyCode.R))
            {
                PlayHaptic(WINDOW_CLOSE_CLIP, RescanButton);
                _dataLoader.Rescan();
            }
        }

        // If permission is granted and there is a scene model, show the lobby menu
        if (InstructionsPrefab.activeSelf)
        {
            if (_dataLoader == null) _dataLoader = SceneApiDataLoaderReference.GetComponent<SceneDataLoader>();

            if (_sceneLoader == null) _sceneLoader = GameSceneLoaderReference.GetComponent<SceneLoader>();

            if (OVRInput.GetDown(StartGameButton) || Input.GetKeyDown(KeyCode.Space))
            {
                PlayHaptic(WINDOW_CLOSE_CLIP, StartGameButton);
                InstructionsPrefab.SetActive(false);
                _sceneLoader.LoadScene();
            }

            if (OVRInput.GetDown(RescanButton) || Input.GetKeyDown(KeyCode.R))
            {
                PlayHaptic(WINDOW_CLOSE_CLIP, RescanButton);
                _dataLoader.Rescan();
            }
        }
    }

    private void PlayHaptic(string clip, OVRInput.RawButton button)
    {
        if (uiHaptics.TryGetPlayer(clip, out var player))
        {
            var controller = Controller.Right;

            if ((button & (OVRInput.RawButton.LHandTrigger
                           | OVRInput.RawButton.LIndexTrigger
                           | OVRInput.RawButton.X
                           | OVRInput.RawButton.Y
                           | OVRInput.RawButton.Start
                           | OVRInput.RawButton.LThumbstick)) != 0)
            {
                controller = Controller.Left;
            }

            player.Play(controller);
        }
    }

    /// <summary>
    /// Activate rescan option if no scene model is available
    /// </summary>
    public void OnNoSceneModelAvailable()
    {
        PermissionParent.SetActive(false);
        PermissionDontAskAgain.SetActive(false);
        InstructionsPrefab.SetActive(false);
        NoSceneModelParent.SetActive(true); // Set this to true
    }

    public void OnNewSceneModelAvailable()
    {
        PermissionParent.SetActive(false);
        PermissionDontAskAgain.SetActive(false);
        InstructionsPrefab.SetActive(true); // Set this to true
        NoSceneModelParent.SetActive(false);
    }

    /// <summary>
    /// Request scene data permission
    /// </summary>
    public void RequestScenePermission()
    {
        if (Permission.HasUserAuthorizedPermission(SCENE_PERMISSION))
        {
            ActOnPermissionGranted();
        }
        else
        {
            PermissionParent.SetActive(true);
            InstructionsPrefab.SetActive(false);

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionDenied += PermissionCallbacks_PermissionDenied;
            callbacks.PermissionGranted += PermissionCallbacks_PermissionGranted;
            callbacks.PermissionDeniedAndDontAskAgain += PermissionCallbacks_PermissionDeniedAndDontAskAgain;
            Permission.RequestUserPermission(SCENE_PERMISSION, callbacks);
        }
    }

    private void OnBoundsChanged(Bounds bounds)
    {
        _boundsSet = true;
        ShowInstructions();
    }

    /// <summary>
    /// Show lobby when permission is granted
    /// </summary>
    private void ActOnPermissionGranted()
    {
        _permissionGranted = true;
        SceneApiDataLoaderReference.SetActive(true);// Set this to true
        GameSceneLoaderReference.SetActive(true);// Set this to true

        ShowInstructions();
    }

    private void ShowInstructions()
    {
        if (!_boundsSet || !_permissionGranted)
        {
            return;
        }

        PermissionParent.SetActive(false);
        PermissionDontAskAgain.SetActive(false);
        InstructionsPrefab.SetActive(true);// Set this to true
        NoSceneModelParent.SetActive(false);
    }

    /// <summary>
    /// Handle denied permission states
    /// </summary>
    /// <param name="dontAskAgain">Whether the user has clicked the 'DontAskAgain' checkbox on the permission dialog</param>
    private void ActOnPermissionDenied(bool dontAskAgain = false)
    {
        if (dontAskAgain)
        {
            PermissionParent.SetActive(false);
            PermissionDontAskAgain.SetActive(true);
        }
        else
        {
            PermissionParent.SetActive(true);
            PermissionDontAskAgain.SetActive(false);
        }

        SceneApiDataLoaderReference.SetActive(false);
        InstructionsPrefab.SetActive(false);
        NoSceneModelParent.SetActive(false);
    }

    internal void PermissionCallbacks_PermissionDeniedAndDontAskAgain(string permissionName)
    {
        Debug.Log($"{permissionName} PermissionDeniedAndDontAskAgain");
        ActOnPermissionDenied(true);
    }

    internal void PermissionCallbacks_PermissionGranted(string permissionName)
    {
        ActOnPermissionGranted();
        Debug.Log($"{permissionName} PermissionCallbacks_PermissionGranted");
    }

    internal void PermissionCallbacks_PermissionDenied(string permissionName)
    {
        Debug.Log($"{permissionName} PermissionCallbacks_PermissionDenied");
        ActOnPermissionDenied();
    }
}
