// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using PhantoUtils;
using UnityEngine;

namespace Phanto.Audio.Scripts
{
    /// <summary>
    /// Manages sound effects for Phanto
    /// </summary>
    public class PhantoGooSfxManager : SingletonMonoBehaviour<PhantoGooSfxManager>
    {
        [SerializeField] private bool startMusicOnLoad;

        [SerializeField] private int numberOfGooSourcesAllowable;
        [SerializeField] private float updateRate = 1;
        [SerializeField] private bool doDebugDraw;

        [SerializeField] private bool stopMusicTest;

        [SerializeField] private int poolCount = 10;
        [SerializeField] private PhantoRandomOneShotSfxBehavior gooBallShootSfxPrefab;
        [SerializeField] private PhantoRandomOneShotSfxBehavior gooStartSfxPrefab;
        [SerializeField] private PhantoRandomOneShotSfxBehavior gooStopSfxPrefab;
        [SerializeField] private PhantoRandomOneShotSfxBehavior gooBallShootVoPrefab;
        [SerializeField] private PhantoRandomOneShotSfxBehavior phantoHurtVoPrefab;

        [Space(16)]
        [SerializeField] private PhantoRandomOneShotSfxBehavior phantoAppearVo;
        [SerializeField] private PhantoRandomOneShotSfxBehavior phantoDieVo;
        [SerializeField] private PhantoLoopSfxBehavior phantoTutorialMusicLoop;
        [SerializeField] private PhantoLoopSfxBehavior phantoMusicLoop;
        [SerializeField] private PhantoLoopSfxBehavior phantoWarningMusicLoop;
        [SerializeField] private AnimationCurve dangerMusicVolume;

        [SerializeField] private SfxContainer[] sfxContainers;

        [SerializeField] private SemanticSoundMapCollection semanticSoundMapCollection;


        private readonly Dictionary<string, (List<PhantoRandomOneShotSfxBehavior> instances, int index)> _sfxDictionary =
            new();

        private readonly List<PhantoRandomOneShotSfxBehavior> _gooBallShootSfx = new();
        private readonly List<PhantoRandomOneShotSfxBehavior> _gooStartSfx = new();
        private readonly List<PhantoRandomOneShotSfxBehavior> _gooStopSfx = new();
        private readonly List<PhantoRandomOneShotSfxBehavior> _gooBallShootVo = new();
        private readonly List<PhantoRandomOneShotSfxBehavior> _phantoHurtVo = new();

        private readonly List<PhantoGoo> _activeGoos = new();

        private readonly Dictionary<string, Queue<PhantoRandomOneShotSfxBehavior>> _semanticSfxDictionary =
            new Dictionary<string, Queue<PhantoRandomOneShotSfxBehavior>>();

        private int _gooBallVoInstanceCount;

        private bool _hurtVoActive;
        private bool _lastStopMusicTest;
        private int _phantoHurtInstanceCount;

        private int _shootSfxInstanceCount;
        private int _startSfxInstanceCount;
        private int _stopSfxInstanceCount;

        private float _dangerLevel;

        public float testlevel = 1;

        protected override void Awake()
        {
            base.Awake();

            _activeGoos.Clear();

            foreach (var container in sfxContainers)
            {
                var instances = new List<PhantoRandomOneShotSfxBehavior>();

                for (var i = 0; i < container.spawnCount; i++)
                {
                    var instance = Instantiate(container.sfxBehavior, transform);
                    instance.gameObject.SetSuffix($"{i:000}");
                    instances.Add(instance);
                }

                _sfxDictionary[container.name] = (instances, 0);
            }

            PopulatePool(gooBallShootSfxPrefab, _gooBallShootSfx, poolCount);
            PopulatePool(gooStartSfxPrefab, _gooStartSfx, poolCount);
            PopulatePool(gooStopSfxPrefab, _gooStopSfx, poolCount);
            PopulatePool(gooBallShootVoPrefab, _gooBallShootVo, poolCount);
            PopulatePool(phantoHurtVoPrefab, _phantoHurtVo, poolCount);

            PopulateSemanticSfxPool(poolCount);
        }

        private void Start()
        {
            StartCoroutine(GooSfxUpdateLoop());
            if (startMusicOnLoad) StartMusic();

            phantoMusicLoop.loopBeginEvent.AddListener(StartWarningLoops);
            SetDangerLevel(0);
            phantoWarningMusicLoop.loopSrc.volume = 0;
        }

        private void Update()
        {
            if (doDebugDraw)
                for (var i = 0; i < numberOfGooSourcesAllowable; i++)
                {
                    if (_activeGoos.Count > i)
                        Debug.DrawLine(_activeGoos[i].transform.position, Camera.main.transform.position, Color.green);
                }

            if (stopMusicTest != _lastStopMusicTest && stopMusicTest) StopMusic();

            _lastStopMusicTest = stopMusicTest;
        }

        public void RegisterGoo(PhantoGoo goo)
        {
            _activeGoos.Add(goo);
        }

        public void UnregisterGoo(PhantoGoo goo)
        {
            _activeGoos.Remove(goo);
        }

        public void PlayGooStartSound(Vector3 position)
        {
            if (_gooStartSfx.Count <= 0) return;

            if (_startSfxInstanceCount >= 0 && _startSfxInstanceCount < _gooStartSfx.Count)
            {
                if (_gooStartSfx[_startSfxInstanceCount] == null) return;
            }
            else
            {
                return;
            }

            _gooStartSfx[_startSfxInstanceCount].transform.position = position;
            _gooStartSfx[_startSfxInstanceCount].PlaySfx();
            _startSfxInstanceCount++;
            _startSfxInstanceCount %= _gooStartSfx.Count - 1;
        }

        public void PlayGooBallShootSound(Vector3 position)
        {
            if (_gooBallShootSfx.Count <= 0) return;

            if (_shootSfxInstanceCount >= 0 && _shootSfxInstanceCount < _gooBallShootSfx.Count)
            {
                if (_gooBallShootSfx[_shootSfxInstanceCount] == null) return;
            }
            else
            {
                return;
            }

            _gooBallShootSfx[_shootSfxInstanceCount].transform.position = position;
            _gooBallShootSfx[_shootSfxInstanceCount].PlaySfx();
            _shootSfxInstanceCount++;
            _shootSfxInstanceCount %= _gooBallShootSfx.Count - 1;
        }

        public void PlayGooStopSound(Vector3 position)
        {
            if (_gooStopSfx.Count <= 0) return;

            if (_stopSfxInstanceCount >= 0 && _stopSfxInstanceCount < _gooStopSfx.Count)
            {
                if (_gooStopSfx[_stopSfxInstanceCount] == null) return;
            }
            else
            {
                return;
            }

            _gooStopSfx[_stopSfxInstanceCount].transform.position = position;
            _gooStopSfx[_stopSfxInstanceCount].PlaySfx();
            _stopSfxInstanceCount++;
            _stopSfxInstanceCount %= _gooStopSfx.Count - 1;
        }

        public void PlayGooBallShootVo(Vector3 position)
        {
            if (_gooBallShootVo.Count <= 0) return;

            if (_gooBallVoInstanceCount >= 0 && _gooBallVoInstanceCount < _gooBallShootVo.Count)
            {
                if (_gooBallShootVo[_gooBallVoInstanceCount] == null) return;
            }
            else
            {
                return;
            }

            _gooBallShootVo[_gooBallVoInstanceCount].transform.position = position;
            _gooBallShootVo[_gooBallVoInstanceCount].PlaySfx();
            _gooBallVoInstanceCount++;
            _gooBallVoInstanceCount %= _gooBallShootVo.Count - 1;
        }

        public void PlayPhantoHurtVo(Vector3 position)
        {
            if (_phantoHurtVo.Count <= 0 || _hurtVoActive) return;

            if (_phantoHurtInstanceCount < 0 || _phantoHurtInstanceCount >= _phantoHurtVo.Count) return;

            if (_phantoHurtVo[_phantoHurtInstanceCount] != null)
            {
                _phantoHurtVo[_phantoHurtInstanceCount].transform.position = position;
                _phantoHurtVo[_phantoHurtInstanceCount].PlaySfx();

                StartCoroutine(WaitForClipToEnd(_phantoHurtVo[_phantoHurtInstanceCount].ClipLength));
                _hurtVoActive = true;
            }

            _phantoHurtInstanceCount++;
            _phantoHurtInstanceCount %= _phantoHurtVo.Count - 1;
        }

        private IEnumerator WaitForClipToEnd(float waitTime)
        {
            yield return new WaitForSeconds(waitTime);
            _hurtVoActive = false;
        }

        public void PlayPhantoDieVo(Vector3 position)
        {
            phantoDieVo.transform.position = position;
            phantoDieVo.PlaySfx();
        }

        public void PlayPhantoAppearVo(Vector3 position)
        {
            phantoAppearVo.transform.position = position;
            phantoAppearVo.PlaySfx();
        }

        public void PlayPhantomSpawnVo(Vector3 position)
        {
            PlaySfxInstanceAtPosition("phantom_spawn", position);
        }

        public void PlayPhantomLaughVo(Vector3 position)
        {
            PlaySfxInstanceAtPosition("phantom_laugh", position);
        }

        public void PlayPhantomDieVo(Vector3 position)
        {
            PlaySfxInstanceAtPosition("phantom_die", position);
        }

        public void PlayPhantomHurtVo(Vector3 position)
        {
            PlaySfxInstanceAtPosition("phantom_hurt", position);
        }

        public void PlayPhantomJumpVo(Vector3 position)
        {
            PlaySfxInstanceAtPosition("phantom_jump", position);
        }

        public void PlayPhantomThoughtBubbleAppear(Vector3 position)
        {
            PlaySfxInstanceAtPosition("phantom_thought_appear", position);
        }

        public void PlayPhantomThoughtBubbleDisappear(Vector3 position)
        {
            PlaySfxInstanceAtPosition("phantom_thought_disappear", position);
        }

        private void PlaySfxInstanceAtPosition(string label, Vector3 position)
        {
            if (!_sfxDictionary.TryGetValue(label, out var sfxItem) ||
                sfxItem.instances.Count == 0)
                return;

            sfxItem.instances[sfxItem.index].PlaySfxAtPosition(position);
            sfxItem.index = (sfxItem.index + 1) % sfxItem.instances.Count;

            _sfxDictionary[label] = sfxItem;
        }

        public void StartMusic()
        {
            phantoMusicLoop.ForceStop();
            phantoMusicLoop.StartSfx(true);
            phantoWarningMusicLoop.ForceStop();
        }

        public void StopMusic(bool force = false)
        {
            if (force)
            {
                phantoMusicLoop.ForceStop();
                phantoWarningMusicLoop.ForceStop();
            }
            else
            {
                phantoMusicLoop.StopSfx();
                phantoWarningMusicLoop.StopSfx();
            }
        }


        public void StartTutorialMusic()
        {
            phantoTutorialMusicLoop.ForceStop();
            phantoTutorialMusicLoop.StartSfx();
        }

        public void StopTutorialMusic(bool force = false)
        {
            if (force)
            {
                phantoTutorialMusicLoop.ForceStop();
            }
            else
            {
                phantoTutorialMusicLoop.StopSfx();
            }
        }

        public void StartWarningLoops()
        {
            phantoWarningMusicLoop.StartSfx();
        }
        public void SetDangerLevel(float level)
        {
            _dangerLevel = Mathf.Lerp(_dangerLevel, level, Time.deltaTime*2);
            phantoWarningMusicLoop.loopSrc.volume = dangerMusicVolume.Evaluate(_dangerLevel);
        }


        /// <summary>
        /// Determine the type of the object the phantom landed on and play an associated sound.
        /// </summary>
        /// <param name="point"></param>
        public void PlayPhantomHopSfx(Vector3 point)
        {
            // Play a sound based on what you landed on.
            if (!SceneQuery.TryGetClosestSemanticClassification(point, Vector3.up, out var semanticClassification))
            {
                return;
            }

            var label = semanticClassification.Labels[0];

            if (string.IsNullOrEmpty(label) || !_semanticSfxDictionary.TryGetValue(label, out var queue) )
            {
                return;
            }

            if (queue.TryDequeue(out var oneShot))
            {
                oneShot.PlaySfxAtPosition(point);
                queue.Enqueue(oneShot);
            }
        }

        private void UpdateGooSfxPositions()
        {
            // Sort by distance
            SortNearestGoos();
            for (var i = 0; i < _activeGoos.Count; i++)
            {
                var currentGoo = _activeGoos[i];

                if (i <= numberOfGooSourcesAllowable)
                {
                    currentGoo.StartSfx();
                }
                else
                {
                    currentGoo.StopSfx();
                }
            }
        }

        private void SortNearestGoos()
        {
            var cameraPosition = Camera.main.transform.position;

            _activeGoos.Sort((t1, t2) =>
                (cameraPosition - t1.Position).sqrMagnitude
                .CompareTo((cameraPosition - t2.Position).sqrMagnitude));
        }

        private IEnumerator GooSfxUpdateLoop()
        {
            while (enabled)
            {
                yield return new WaitForSeconds(updateRate);
                UpdateGooSfxPositions();
            }
        }

        private void PopulatePool(PhantoRandomOneShotSfxBehavior prefab, List<PhantoRandomOneShotSfxBehavior> pool, int size)
        {
            var parent = transform;

            for (int i = 0; i < pool.Count; i++)
            {
                Destroy(pool[i].gameObject);
            }

            pool.Clear();

            for (int i = 0; i < size; i++)
            {
                var instance = Instantiate(prefab, parent);
                instance.gameObject.SetSuffix($"{i:000}");
                pool.Add(instance);
            }
        }

        private void PopulateSemanticSfxPool(int size)
        {
            foreach (var queue in _semanticSfxDictionary.Values)
            {
                foreach (var entry in queue)
                {
                    Destroy(entry.gameObject);
                }
                queue.Clear();
            }
            _semanticSfxDictionary.Clear();

            var parent = transform;

            var count = 0;

            foreach (var soundMap in semanticSoundMapCollection.SoundMaps)
            {
                var label = soundMap.name;
                var prefab = soundMap.oneShotPrefab;

                if (string.IsNullOrEmpty(label) || prefab == null)
                {
                    continue;
                }

                var queue = new Queue<PhantoRandomOneShotSfxBehavior>();

                for (int i = 0; i < size; i++)
                {
                    var instance = Instantiate(prefab, parent);
                    instance.gameObject.SetSuffix($"OneShot {count++:000}");
                    queue.Enqueue(instance);
                }

                _semanticSfxDictionary.TryAdd(label, queue);
            }
        }

        [Serializable]
        public class SfxContainer
        {
            public string name;
            public PhantoRandomOneShotSfxBehavior sfxBehavior;
            public int spawnCount = 8;
        }
    }
}
