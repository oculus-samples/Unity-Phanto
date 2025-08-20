// Copyright (c) Meta Platforms, Inc. and affiliates.

using Phantom;
using PhantoUtils;
using UnityEngine;

namespace Phanto.Enemies.DebugScripts
{
    public class EnemyDebugManager : MonoBehaviour
    {
        [SerializeField] private PhantomManager phantomManager;
        [SerializeField] private Phanto phanto;
        [SerializeField] private GameObject gooPrefab;
        private bool _phantoActive = true;

        private bool _phantomManagerActive = true;

        private void Update()
        {
            var startIsDown = OVRInput.Get(OVRInput.Button.Start,
                OVRInput.Controller.LTouch | OVRInput.Controller.RTouch);

            var gripIsDown = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger,
                OVRInput.Controller.LTouch | OVRInput.Controller.RTouch);

            if (startIsDown && gripIsDown &&
                OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickDown, OVRInput.Controller.LTouch))
            {
                TogglePhanto();
            }

            if (startIsDown && gripIsDown &&
                OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickLeft, OVRInput.Controller.LTouch))
            {
                // Toggle phantom manager on and off
                _phantomManagerActive = !_phantomManagerActive;

                phantomManager.enabled = _phantomManagerActive;
            }

            if (startIsDown && gripIsDown &&
                OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickRight, OVRInput.Controller.LTouch))
            {
                SpawnRandomGoo();
            }

#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F2))
            {
                TogglePhanto();
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                SpawnRandomGoo();
            }

            if (Input.GetKeyDown(KeyCode.F4))
            {
                ExtinguishAllGoo();
            }
#endif
        }

        private void TogglePhanto()
        {
            // Toggle Phanto on and off
            _phantoActive = !_phantoActive;
            phanto.Show(_phantoActive);
        }

        private void SpawnRandomGoo()
        {
            // Start a random goo on floor/furniture.
            var triangle = NavMeshBookKeeper.GetAllTriangles().RandomElement();

            PoolManagerSingleton.Instance.Create(gooPrefab, triangle.GetRandomPoint(),
                Quaternion.LookRotation(Vector3.up));
        }

        private void ExtinguishAllGoo()
        {
            // Clean the whole room at once
            foreach (var goo in PhantoGoo.ActiveGoos)
            {
                goo.Extinguish();
            }
        }
    }
}
