// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using UnityEngine;

namespace Phantom.Environment.Scripts
{
    /// <summary>
    /// A simulated object in the scene
    /// </summary>
    public class SimulatedObject : MonoBehaviour
    {
        public enum Classification
        {
            Floor,
            Ceiling,
            WallFace,
            Desk,
            Couch,
            DoorFrame,
            WindowFrame,
            Other,
            Storage,
            Bed,
            Screen,
            Lamp,
            Plant,
            Table,
            Chair,
            WallArt,
            WallOpening,
            GlobalMesh
        }

        [SerializeField] private Classification Class;


        private readonly Dictionary<Classification, string> classesDict = new()
        {
            { Classification.Floor, OVRSceneManager.Classification.Floor },
            { Classification.Ceiling, OVRSceneManager.Classification.Ceiling },
            { Classification.WallFace, OVRSceneManager.Classification.WallFace },
            { Classification.Desk, OVRSceneManager.Classification.Table },
            { Classification.Couch, OVRSceneManager.Classification.Couch },
            { Classification.DoorFrame, OVRSceneManager.Classification.DoorFrame },
            { Classification.WindowFrame, OVRSceneManager.Classification.WindowFrame },
            { Classification.Other, OVRSceneManager.Classification.Other },
            { Classification.Storage, OVRSceneManager.Classification.Storage },
            { Classification.Bed, OVRSceneManager.Classification.Bed },
            { Classification.Screen, OVRSceneManager.Classification.Screen },
            { Classification.Lamp, OVRSceneManager.Classification.Lamp },
            { Classification.Plant, OVRSceneManager.Classification.Plant },
            { Classification.Table, OVRSceneManager.Classification.Table },
            { Classification.WallArt, OVRSceneManager.Classification.WallArt },
            { Classification.GlobalMesh, OVRSceneManager.Classification.GlobalMesh }
        };


        public string CurrentClass => classesDict[Class];
    }
}
