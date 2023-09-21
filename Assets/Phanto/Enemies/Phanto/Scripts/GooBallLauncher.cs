// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

public class GooBallLauncher : MonoBehaviour
{
    /// <summary>
    /// Controls spawning of the GooBall.
    /// </summary>
    public GameObject gooBallPrefab;

    [SerializeField] private OVRInput.RawButton _triggerButton;
    public float force = 10;

    private void Update()
    {
        // Spawn the GooBall if pressing the trigger button.
        if (OVRInput.GetDown(_triggerButton) || Input.GetKeyDown(KeyCode.T))
        {
            var newGooBall = Instantiate(gooBallPrefab, transform.position, Quaternion.identity);
            newGooBall.GetComponent<Rigidbody>().AddForce(transform.forward * force);
        }
    }
}
