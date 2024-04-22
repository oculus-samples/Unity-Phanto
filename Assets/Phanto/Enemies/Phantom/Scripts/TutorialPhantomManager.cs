// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using Phantom.Environment.Scripts;
using PhantoUtils;
using PhantoUtils.VR;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace Phantom
{
    class TutorialPhantomManager : PhantomManager
    {
        private Coroutine _tutorialCoroutine;
        private GameplaySettings.WinCondition _winCondition;

        public bool MobTutorialComplete { get; private set; }

        private readonly List<PhantomTarget> _tutorialTargets = new List<PhantomTarget>();
        private readonly List<PhantomController> _tutorialPhantoms = new List<PhantomController>();

        protected override void OnEnable()
        {
            SceneBoundsChecker.BoundsChanged += OnBoundsChanged;
            settingsManager.OnWaveAdvance += OnWaveAdvance;
        }

        protected override void Init()
        {
            WinCondition = GameplaySettings.WinCondition.DefeatAllPhantoms;

            MobTutorialComplete = false;

            var parent = transform;

            for (var i = 0; i < fleeInstances; i++)
            {
                var ouch = Instantiate(fleaTargetPrefab, parent);
                _allPhantomTargets.Add(ouch);
                ouch.gameObject.SetSuffix($"{i:00}");
                ouch.Hide();
                _ouchPool.Enqueue(ouch);
            }
        }

        public void ActivateTutorialPage(int page, bool visible)
        {
            if (visible)
            {
                settingsManager.SetWave(page);
            }
        }

        public void ActivateMobTutorial(bool visible, GameplaySettings.WinCondition winCondition)
        {
            if (visible) MobTutorialComplete = false;
            if (_tutorialCoroutine != null)
            {
                StopCoroutine(_tutorialCoroutine);
                _tutorialCoroutine = null;
            }

            CleanTheStage();
            _winCondition = winCondition;

            switch (winCondition)
            {
                case GameplaySettings.WinCondition.DefeatPhanto:
                    if (visible && !MobTutorialComplete)
                    {
                        _tutorialCoroutine = StartCoroutine(PhantoTutorial());
                    }

                    break;
                case GameplaySettings.WinCondition.DefeatAllPhantoms:
                    // create an table crystal in front of user
                    // create an origin crystal in front of user
                    _phantomWaveCount = 4;

                    if (visible && !MobTutorialComplete)
                    {
                        _tutorialCoroutine = StartCoroutine(CrystalTutorial());
                    }

                    break;
            }
        }

        public override void DecrementPhantom()
        {
            // TODO: adjust the phantom rounds win conditions
            if (--_phantomWaveCount <= 0)
            {
                RoundOver();
            }
        }

        public void OnNewWave(GameplaySettings.WaveSettings waveSettings)
        {
        }

        protected override void OnWaveAdvance()
        {
            if (_winCondition == GameplaySettings.WinCondition.DefeatPhanto)
            {
                MobTutorialComplete = true;
            }
        }

        protected override void RoundOver(bool victory = true)
        {
            MobTutorialComplete = true;
        }

        private IEnumerator PhantoTutorial()
        {
            Debug.Log($"[{nameof(TutorialPhantomManager)}] [{nameof(PhantoTutorial)}]");

            var playerHeadPos = CameraRig.Instance.CenterEyeAnchor.position;

            // find a spawn point for phanto.
            var spawnPoint = FindPhantoSpawnPosition(playerHeadPos);
            phanto.transform.position = spawnPoint;

            // activate phanto
            yield return new WaitForSeconds(1.0f);
            phanto.gameObject.SetActive(true);

            var wait = new WaitForSeconds(1.0f);

            var spread = new Vector2(0.06f, 0.06f);

            // scatter some goo particles.
            for (int i = 0; i < 5; i++)
            {
                yield return wait;

                phanto.ShootGooball(1, spread);
            }
        }

        private IEnumerator CrystalTutorial()
        {
            Debug.Log($"[{nameof(TutorialPhantomManager)}] [{nameof(CrystalTutorial)}]");

            _phantomWaveCount = 5;
            _spawnWait = new WaitForSeconds(1.5f);

            // spawn window/door crystals.
            // spawn a table crystal
            ActivateCrystals();

            // Create a spawn point that can't be removed for the phantoms.
            CreateSpawnPoints(1);

            // any existing phantoms need to have their attack delays reset and clear any goa
            foreach (var phantom in _activePhantoms)
            {
                phantom.ResetState();
            }

            if (_phantomSpawnerCoroutine == null)
            {
                _phantomSpawnerCoroutine = StartCoroutine(PhantomSpawner());
            }

            yield break;
        }

        private void CleanTheStage()
        {
            if (_phantomSpawnerCoroutine != null)
            {
                StopCoroutine(_phantomSpawnerCoroutine);
                _phantomSpawnerCoroutine = null;
            }

            if (_tableCrystalInstance != null)
            {
                _tableCrystalInstance.Destruct();
            }

            DestroyOriginCrystals();

            _tutorialTargets.Clear();
            _tutorialTargets.AddRange(PhantomTarget.AvailableTargets);

            // return all targets currently in the room
            for (var i = 0; i < _tutorialTargets.Count; i++)
            {
                _tutorialTargets[i].Hide();
            }

            _tutorialPhantoms.Clear();
            _tutorialPhantoms.AddRange(_activePhantoms);

            for (var i = 0; i < _tutorialPhantoms.Count; i++)
            {
                _tutorialPhantoms[i].Hide();
            }

            RemoveGoo();

            phanto.gameObject.SetActive(false);
        }

        protected override void CreateSpawnPoints(int count)
        {
            Assert.IsNotNull(_sceneRoom);

            if (_originCrystals.Count > 0)
            {
                DestroyOriginCrystals();
            }

            var destination = _tableCrystalInstance.Position;

            // create spawn point(s) that's ~2m away from the table crystal so player
            // doesn't need to hunt for it during the tutorial.

            var ceilingPlane = SceneQuery.GetLid(_sceneRoom);

            var floorTransform = _sceneRoom.Floor.transform;
            var floorPlane = new Plane(floorTransform.forward, floorTransform.position);

            var spawnPoints = new List<(Vector3 point, float pathLength)>();

            var floorPos = floorPlane.ClosestPointOnPlane(destination);

            for (int i = 0; i < 1000; i++)
            {
                var point = floorPlane.ClosestPointOnPlane(floorPos + Random.onUnitSphere * 2.0f);

                if (!NavMesh.SamplePosition(point, out var hit, 1.0f, NavMesh.AllAreas) ||
                    !SceneQuery.VerifyPointIsOpen(point, ceilingPlane))
                {
                    continue;
                }

                point = hit.position;

                spawnPoints.Add((point, Vector3.Distance(point, floorPos)));

                if (spawnPoints.Count > Mathf.Max(16, count * 2))
                {
                    break;
                }
            }

            Assert.IsTrue(spawnPoints.Count >= count);

            // sort spawn points by path length (longest first);
            spawnPoints.Sort((a, b) => b.pathLength.CompareTo(a.pathLength));

            for (int i = 0; i < count; i++)
            {
                SpawnCrystal(spawnPoints[i].point);
            }
        }
    }
}
