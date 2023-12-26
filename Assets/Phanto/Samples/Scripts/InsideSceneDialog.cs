// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Utilities.XR;
using Phanto.Enemies.DebugScripts;
using Phantom.Environment.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;

public class InsideSceneDialog : MonoBehaviour
{
    private const string In = "In";
    private const string Out = "<color=#FF0000>Out</color>";

    [SerializeField] private SceneDataLoader sceneDataLoader;

    [SerializeField] private TextMeshProUGUI infoText;

    [SerializeField] private TextMeshProUGUI buttonText;

    [SerializeField] private GameObject buttonGameObject;

    [SerializeField] private bool debugDraw = true;

    private OVRCameraRig _cameraRig;
    private bool _ready;
    private bool _insideScene = true;

    private bool _leftHandIn;
    private bool _rightHandIn;
    private bool _headIn;

    protected void Awake()
    {
        Assert.IsNotNull(sceneDataLoader);

        DebugDrawManager.DebugDraw = debugDraw;
        buttonGameObject.SetActive(false);
    }

    private void OnEnable()
    {
        InsideSceneChecker.UserInSceneChanged += OnUserInSceneChanged;
        DebugDrawManager.DebugDrawEvent += DebugDraw;

        StartCoroutine(CheckLimbsInBounds());
    }

    private void OnDisable()
    {
        InsideSceneChecker.UserInSceneChanged -= OnUserInSceneChanged;
        DebugDrawManager.DebugDrawEvent -= DebugDraw;
    }

    private IEnumerator Start()
    {
        while (OVRManager.instance == null)
        {
            yield return null;
        }

        if (!OVRManager.instance.TryGetComponent(out _cameraRig))
        {
            _cameraRig = FindObjectOfType<OVRCameraRig>();
        }

        _ready = _cameraRig != null;
    }

    private void Update()
    {
        // While the user is outside of the room we give them the option to rescan their space.
        if (!_insideScene && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
        {
            sceneDataLoader.Rescan();
        }
    }

    private IEnumerator CheckLimbsInBounds()
    {

        // check 5 times a second.
        var wait = new WaitForSeconds(0.2f);

        while (!_ready)
        {
            yield return null;
        }

        while (enabled)
        {
            _leftHandIn = InsideSceneChecker.PointInsideScene(_cameraRig.leftHandAnchor.position);
            _rightHandIn = InsideSceneChecker.PointInsideScene(_cameraRig.rightHandAnchor.position);
            _headIn = InsideSceneChecker.PointInsideScene(_cameraRig.centerEyeAnchor.position);

            var statusText = $"Head: {(_headIn ? In : Out) }\nLeft controller: {(_leftHandIn ? In : Out)}\nRight controller: {(_rightHandIn ? In : Out)}";

            infoText.text = statusText;

            yield return wait;
        }
    }

    private void DebugDraw()
    {
        if (!_ready)
        {
            return;
        }

        DebugDrawHand(_cameraRig.rightHandAnchor, _rightHandIn);
        DebugDrawHand(_cameraRig.leftHandAnchor, _leftHandIn);
    }

    private void DebugDrawHand(Transform hand, bool inBounds)
    {
        XRGizmos.DrawSphere(hand.position, 0.05f, inBounds ? Color.green : Color.red);
    }

    private void OnUserInSceneChanged(Bounds bounds, bool inside)
    {
        if (_insideScene == inside)
        {
            return;
        }

        _insideScene = inside;
        buttonGameObject.SetActive(!_insideScene);
    }
}
