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

        public int numberOfGooSourcesAllowable;
        public float updateRate = 1;
        public bool doDebugDraw;

        public bool stopMusicTest;

        public List<PhantoGoo> activeGoos = new();

        public PhantoRandomOneshotSfxBehavior[] gooBallShootSfx;
        public PhantoRandomOneshotSfxBehavior[] gooStartSfx;
        public PhantoRandomOneshotSfxBehavior[] gooStopSfx;
        public PhantoRandomOneshotSfxBehavior[] gooBallShootVO;
        public PhantoRandomOneshotSfxBehavior[] phantoHurtVO;
        public PhantoRandomOneshotSfxBehavior phantoAppearVo;
        public PhantoRandomOneshotSfxBehavior phantoDieVo;
        public PhantoLoopSfxBehavior phantoMusicLoop;

        [SerializeField] private SfxContainer[] sfxContainers;

        private readonly Dictionary<string, (List<PhantoRandomOneshotSfxBehavior> instances, int index)> sfxDictionary =
            new();

        private int gooBallVOInstanceCount;

        private bool hurtVoActive;
        private bool lastStopMusicTest;
        private int phantoHurtInstanceCount;

        private int shootSfxInstanceCount;
        private int startSfxInstanceCount;
        private int stopSfxInstanceCount;

        protected override void Awake()
        {
            base.Awake();

            activeGoos.Clear();

            foreach (var container in sfxContainers)
            {
                var instances = new List<PhantoRandomOneshotSfxBehavior>();

                for (var i = 0; i < container.spawnCount; i++)
                {
                    instances.Add(Instantiate(container.sfxBehavior, transform));
                }

                sfxDictionary[container.name] = (instances, 0);
            }
        }

        private void Start()
        {
            StartCoroutine(GooSfxUpdateLoop());
            if (startMusicOnLoad) StartMusic();
        }

        private void Update()
        {
            if (doDebugDraw)
                for (var i = 0; i < numberOfGooSourcesAllowable; i++)
                {
                    if (activeGoos.Count > i)
                        Debug.DrawLine(activeGoos[i].transform.position, Camera.main.transform.position, Color.green);
                }

            if (stopMusicTest != lastStopMusicTest && stopMusicTest) StopMusic();

            lastStopMusicTest = stopMusicTest;
        }

        public void PlayGooStartSound(Vector3 position)
        {
            if (gooStartSfx.Length <= 0) return;

            if (startSfxInstanceCount >= 0 && startSfxInstanceCount < gooStartSfx.Length)
            {
                if (gooStartSfx[startSfxInstanceCount] == null) return;
            }
            else
            {
                return;
            }

            gooStartSfx[startSfxInstanceCount].transform.position = position;
            gooStartSfx[startSfxInstanceCount].PlaySfx();
            startSfxInstanceCount++;
            startSfxInstanceCount %= gooStartSfx.Length - 1;
        }

        public void PlayGooBallShootSound(Vector3 position)
        {
            if (gooBallShootSfx.Length <= 0) return;

            if (shootSfxInstanceCount >= 0 && shootSfxInstanceCount < gooBallShootSfx.Length)
            {
                if (gooBallShootSfx[shootSfxInstanceCount] == null) return;
            }
            else
            {
                return;
            }

            gooBallShootSfx[shootSfxInstanceCount].transform.position = position;
            gooBallShootSfx[shootSfxInstanceCount].PlaySfx();
            shootSfxInstanceCount++;
            shootSfxInstanceCount %= gooBallShootSfx.Length - 1;
        }

        public void PlayGooStopSound(Vector3 position)
        {
            if (gooStopSfx.Length <= 0) return;

            if (stopSfxInstanceCount >= 0 && stopSfxInstanceCount < gooStopSfx.Length)
            {
                if (gooStopSfx[stopSfxInstanceCount] == null) return;
            }
            else
            {
                return;
            }

            gooStopSfx[stopSfxInstanceCount].transform.position = position;
            gooStopSfx[stopSfxInstanceCount].PlaySfx();
            stopSfxInstanceCount++;
            stopSfxInstanceCount %= gooStopSfx.Length - 1;
        }

        public void PlayGooBallShootVO(Vector3 position)
        {
            if (gooBallShootVO.Length <= 0) return;

            if (gooBallVOInstanceCount >= 0 && gooBallVOInstanceCount < gooBallShootVO.Length)
            {
                if (gooBallShootVO[gooBallVOInstanceCount] == null) return;
            }
            else
            {
                return;
            }

            gooBallShootVO[gooBallVOInstanceCount].transform.position = position;
            gooBallShootVO[gooBallVOInstanceCount].PlaySfx();
            gooBallVOInstanceCount++;
            gooBallVOInstanceCount %= gooBallShootVO.Length - 1;
        }

        public void PlayPhantoHurtVo(Vector3 position)
        {
            if (phantoHurtVO.Length <= 0 || hurtVoActive) return;

            if (phantoHurtInstanceCount < 0 || phantoHurtInstanceCount >= phantoHurtVO.Length) return;

            if (phantoHurtVO[phantoHurtInstanceCount] != null)
            {
                phantoHurtVO[phantoHurtInstanceCount].transform.position = position;
                phantoHurtVO[phantoHurtInstanceCount].PlaySfx();

                StartCoroutine(WaitForClipToEnd(phantoHurtVO[phantoHurtInstanceCount].ClipLength));
                hurtVoActive = true;
            }

            phantoHurtInstanceCount++;
            phantoHurtInstanceCount %= phantoHurtVO.Length - 1;
        }

        private IEnumerator WaitForClipToEnd(float waitTime)
        {
            yield return new WaitForSeconds(waitTime);
            hurtVoActive = false;
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

        public void PlayMinionSpawnVo(Vector3 position)
        {
            PlaySfxInstanceAtPosition("minion_spawn", position);
        }

        public void PlayMinionLaughVo(Vector3 position)
        {
            PlaySfxInstanceAtPosition("minion_laugh", position);
        }

        public void PlayMinionDieVo(Vector3 position)
        {
            PlaySfxInstanceAtPosition("minion_die", position);
        }

        public void PlayMinionHurtVo(Vector3 position)
        {
            PlaySfxInstanceAtPosition("minion_hurt", position);
        }

        public void PlayMinionJumpVo(Vector3 position)
        {
            PlaySfxInstanceAtPosition("minion_jump", position);
        }

        private void PlaySfxInstanceAtPosition(string label, Vector3 position)
        {
            if (!sfxDictionary.TryGetValue(label, out var sfxItem) ||
                sfxItem.instances.Count == 0)
                return;

            sfxItem.instances[sfxItem.index].PlaySfxAtPosition(position);
            sfxItem.index = (sfxItem.index + 1) % sfxItem.instances.Count;

            sfxDictionary[label] = sfxItem;
        }

        public void StartMusic()
        {
            phantoMusicLoop.ForceStop();
            phantoMusicLoop.StartSfx(true);
        }

        public void StopMusic(bool force = false)
        {
            if (force)
                phantoMusicLoop.ForceStop();
            else
                phantoMusicLoop.StopSfx();
        }

        private void UpdateGooSfxPositions()
        {
            // Sort by distance
            SortNearestGoos();
            for (var i = 0; i < activeGoos.Count; i++)
            {
                if (i <= numberOfGooSourcesAllowable)
                {
                    if (!activeGoos[i].gooLoopSfx.isOn) activeGoos[i].gooLoopSfx.StartSfx();
                }
                else
                {
                    if (activeGoos[i].gooLoopSfx.isOn) activeGoos[i].gooLoopSfx.StopSfx();
                }
            }
        }

        private void SortNearestGoos()
        {
            activeGoos.Sort((t1, t2) => Vector3.Distance(Camera.main.transform.position, t1.transform.position)
                .CompareTo(Vector3.Distance(Camera.main.transform.position, t2.transform.position)));
        }

        private IEnumerator GooSfxUpdateLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(updateRate);
                UpdateGooSfxPositions();
            }
        }

        [Serializable]
        public class SfxContainer
        {
            public string name;
            public PhantoRandomOneshotSfxBehavior sfxBehavior;
            public int spawnCount = 8;
        }
    }
}
