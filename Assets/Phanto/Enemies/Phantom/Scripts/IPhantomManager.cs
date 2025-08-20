// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Phantom
{
    public interface IPhantomManager
    {
        GameplaySettings.WinCondition WinCondition { get; }

        public void SpawnOuch(Vector3 position, Vector3 normal);
        public void DecrementPhantom();
        public void CreateNavMeshLink(Vector3[] pathCorners, int pathCornerCount, Vector3 destination, int areaId);
        public void ReturnToPool(PhantomController phantomController);
    }
}
