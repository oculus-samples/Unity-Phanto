// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using PhantoUtils;
using UnityEngine;

namespace Phantom
{
    /// <summary>
    /// stores a queue of targets.
    /// phantoms spawned from this crystal are assigned targets from this queue.
    /// </summary>
    [SelectionBase]
    public class OriginCrystal : Crystal
    {
        private static readonly HashSet<OriginCrystal> OriginCrystals = new HashSet<OriginCrystal>();

        private readonly List<ICrystalTarget> crystals = new List<ICrystalTarget>(64);
        private readonly Queue<ICrystalTarget> _targets = new Queue<ICrystalTarget>(64);
        private PhantomTarget _currentTarget;

        private readonly HashSet<PhantomController> _spawnedPhantoms = new HashSet<PhantomController>();

        public event Action TargetsDestroyed;

        private void OnEnable()
        {
            OriginCrystals.Add(this);
        }

        private void OnDisable()
        {
            OriginCrystals.Remove(this);
        }

        public void Initialize()
        {
            FindTargets();
        }

        public void RegisterPhantom(PhantomController phantomController)
        {
            _spawnedPhantoms.Add(phantomController);
            if (_currentTarget != null && _currentTarget.Valid)
            {
                phantomController.SetCrystalTarget(_currentTarget);
            }
        }

        public void UnregisterPhantom(PhantomController phantomController)
        {
            _spawnedPhantoms.Remove(phantomController);
        }

        private bool FindTargets()
        {
            crystals.Clear();
            foreach (var target in PhantomTarget.AvailableTargets)
            {
                if (target is ICrystalTarget crystal && !crystals.Contains(crystal))
                {
                    crystals.Add(crystal);
                }
            }

            crystals.Shuffle();
            _targets.Clear();

            foreach (var crystal in crystals)
            {
                _targets.Enqueue(crystal);
            }

            return _targets.Count > 0;
        }

        public void SelectSquadTarget()
        {
            if (_currentTarget == null || !_currentTarget.Valid)
            {
                if (!TryGetCrystalTarget(out var target))
                {
                    // wave is over.
                    TargetsDestroyed?.Invoke();
                }

                _currentTarget = target;
            }

            foreach (var phantom in _spawnedPhantoms)
            {
                phantom.SetCrystalTarget(_currentTarget);
            }
        }

        private bool TryGetCrystalTarget(out PhantomTarget result)
        {
            if (_targets.Count == 0)
            {
                FindTargets();
            }

            result = null;
            var count = _targets.Count;

            while (count-- > 0)
            {
                var target = _targets.Dequeue();
                // put back at the end of the queue.
                _targets.Enqueue(target);

                if (target.Valid)
                {
                    // notify all spawned phantoms of their new target.
                    result = (PhantomTarget)target;
                    return true;
                }
            }

            return false;
        }

        public static OriginCrystal GetClosestOrigin(Vector3 position)
        {
            var minDistance = float.MaxValue;
            OriginCrystal result = null;

            foreach (var crystal in OriginCrystals)
            {
                var sqrMag = (crystal.Position - position).sqrMagnitude;
                if (sqrMag < minDistance)
                {
                    minDistance = sqrMag;
                    result = crystal;
                }
            }

            return result;
        }
    }
}
