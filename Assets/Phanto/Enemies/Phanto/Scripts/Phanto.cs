// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using Phanto.Audio.Scripts;
using Phantom.LightEffects.Scripts;
using PhantoUtils;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

namespace Phanto
{
    [RequireComponent(typeof(Enemy))]
    public class Phanto : MonoBehaviour
    {
        public const string HIT = "HIT";
        private const string SPIT_PARAM = "Spit";
        private const string HIT_PARAM = "Hit";

        public const int MAX_TRAILS = 32;

        public static readonly int spitParamId = Animator.StringToHash(SPIT_PARAM);
        public static readonly int hitParamId = Animator.StringToHash(HIT_PARAM);

        private static readonly Vector3[] trailDirs = new Vector3[MAX_TRAILS];
        private static readonly float[] trailTargetAngs = new float[MAX_TRAILS];
        private static readonly Vector3[] trailTargetDirs = new Vector3[MAX_TRAILS];
        private static readonly Vector3[] trailLastHits = new Vector3[MAX_TRAILS];
        private static readonly Vector3 forwardDown = (Vector3.forward + Vector3.down).normalized;

        [Tooltip("The reference to the GooBall prefab.")]
        [SerializeField] private GameObject gooBallPrefab;

        [Tooltip("The reference to the mouth transform.")]
        [SerializeField] private Transform mouth;

        [SerializeField] internal Animator animator;

        [SerializeField] private LayerMask ectoBlasterLayer;

        [SerializeField] private GameplaySettingsManager settingsManager;

        private readonly List<ParticleCollisionEvent> pces = new(1024);
        private IDamageable[] _damageables;
        private PhantoLightEffect _phantoLightEffect;
        private SkinnedMeshRenderer[] _skinnedMeshRenderers;
        private Enemy enemy;

        public int Wave => settingsManager.Wave;

        public GameObject GooBallPrefab => gooBallPrefab;

        private PoolManagerSingleton poolManager;

        private void Awake()
        {
            enemy = GetComponent<Enemy>();
            _damageables = GetComponents<IDamageable>();
            _skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            StartCoroutine(GetShadow());
        }

        private void Start()
        {
            poolManager = PoolManagerSingleton.Instance;
        }

        private void OnDestroy()
        {
            if (_phantoLightEffect != null)
            {
                _phantoLightEffect.Unregister(transform);
            }
        }

        private void OnParticleCollision(GameObject other)
        {
            if (!other.TryGetComponent(out ParticleSystem ps)) return;

            var accumulatedDamage = 0f;
            var avgPCIntersection = new Vector3();
            var avgPCNormal = new Vector3();
            var sumPCVelocity = new Vector3();
            var count = ps.GetCollisionEvents(gameObject, pces);

            for (var i = 0; i < count; i++)
            {
                var pce = pces[i];

                accumulatedDamage += GameplaySettingsManager.Instance.gameplaySettings.CurrentPhantoSetting.splashDamage;
                avgPCNormal += pce.normal;
                avgPCIntersection += pce.intersection;
                sumPCVelocity += pce.velocity;
            }

            avgPCIntersection /= count;
            avgPCNormal /= count;

            foreach (var damageable in _damageables)
                damageable.TakeDamage(accumulatedDamage, avgPCIntersection, avgPCNormal);

            enemy.rigidbody.AddForce(sumPCVelocity * GameplaySettingsManager.Instance.gameplaySettings.CurrentPhantoSetting.splashAmount);

            var isEctoBlaster = ectoBlasterLayer == (ectoBlasterLayer | (1 << other.layer));

            if (!isEctoBlaster)
            {
                PolterblastTrigger.DamageNotification(accumulatedDamage, avgPCIntersection, avgPCNormal);
            }
        }

        private IEnumerator GetShadow()
        {
            do
            {
                _phantoLightEffect = FindObjectOfType<PhantoLightEffect>();
                yield return null;
            } while (_phantoLightEffect == null);

            _phantoLightEffect.Register(transform);
        }

        public void AdvanceWave()
        {
            settingsManager.AdvanceWave();
        }

        public void ShootGooball(uint numGooballs, Vector2 spread)
        {
            var shotOrigin = mouth.position;
            PhantoGooSfxManager.Instance.PlayGooBallShootSound(shotOrigin);
            PhantoGooSfxManager.Instance.PlayGooBallShootVo(shotOrigin);

            animator.SetTrigger(spitParamId);

            for (uint u = 0; u < numGooballs; ++u)
            {
                var shotDir = mouth.TransformDirection(WeaponUtils.RandomSpread(spread));
                var q = new Quaternion();
                q.SetLookRotation(shotDir);
                var projectile = PoolManagerSingleton.Instance.Create(gooBallPrefab,
                    shotOrigin,
                    q);
                var gooBallSpeed = GameplaySettingsManager.Instance.gameplaySettings.CurrentPhantoSetting.gooBallSpeed;
                projectile.GetComponent<Rigidbody>().LaunchProjectile(shotOrigin, q * Vector3.forward * Random.Range(gooBallSpeed.x, gooBallSpeed.y));
            }
        }

        public Color GetEmissionColor()
        {
            return GetComponentInChildren<SkinnedMeshRenderer>().material.GetColor("_EmissionColor");
        }

        public void SetEmissionColor(Color c)
        {
            foreach (var r in _skinnedMeshRenderers) r.material.SetColor("_EmissionColor", c);
        }

        public void SetAlpha(float alpha)
        {
            foreach (var r in _skinnedMeshRenderers)
                r.material.color = new Color(r.material.color.r,
                    r.material.color.g,
                    r.material.color.b,
                    alpha);
        }

        public void Show(bool visible = true)
        {
            gameObject.SetActive(visible);
        }

        public void Hide()
        {
            Show(false);
        }

        /// <summary>
        /// Toggle the ethereal status of the enemy
        /// </summary>
        public IEnumerator ToggleEthereal(float castTime)
        {
            enemy.invulnerable = !enemy.invulnerable;

            if (!enemy.invulnerable) PhantoGooSfxManager.Instance.PlayPhantoAppearVo(enemy.transform.position);

            var time = castTime;
            while (time > 0f)
            {
                var t = time / castTime;
                SetAlpha(enemy.invulnerable ? t : 1f - t);
                time -= Time.fixedDeltaTime;
                yield return null;
            }

            SetAlpha(enemy.invulnerable ? 0f : 1f);
        }

        /// <summary>
        /// Start a goo cast from nova
        /// </summary>
        public IEnumerator CastGooNova(int numTrails,
            float minSqrDist,
            float castTime,
            int layerMask)
        {
            numTrails = Mathf.Min(numTrails, MAX_TRAILS);
            var angDist = 360f / numTrails;

            for (var i = 0; i < numTrails; ++i)
            {
                trailDirs[i] = Vector3.down;
                trailTargetAngs[i] = Random.Range(0f, angDist) + i * angDist;
                trailTargetDirs[i] = Quaternion.AngleAxis(trailTargetAngs[i], Vector3.up) * forwardDown;
                trailLastHits[i] = Vector3.positiveInfinity;
            }

            var time = 0.5f * castTime;
            var rcpMaxTime = 1f / time;
            while (time > 0f)
            {
                for (var i = 0; i < numTrails; ++i)
                {
                    trailDirs[i] = Quaternion.Euler(Random.Range(-3f, 3f),
                        Random.Range(-3f, 3f),
                        Random.Range(-3f, 3f)) * trailDirs[i];

#if UNITY_EDITOR
                    Debug.DrawRay(enemy.rigidbody.position,
                        Vector3.Lerp(trailTargetDirs[i], trailDirs[i], time * rcpMaxTime) * 100f,
                        Color.red,
                        0.1f,
                        false);
#endif
                    if (Physics.Raycast(enemy.rigidbody.position,
                            Vector3.Lerp(trailTargetDirs[i], trailDirs[i], time),
                            out var hit,
                            Mathf.Infinity,
                            layerMask,
                            QueryTriggerInteraction.Ignore) &&
                        (hit.point - trailLastHits[i]).sqrMagnitude > minSqrDist)
                    {
                        poolManager.StartGoo(hit.point, Quaternion.identity);
                        trailLastHits[i] = hit.point;
                        yield return null;
                    }
                }

                time -= Time.fixedDeltaTime;
                yield return null;
            }

            var tmp = trailTargetDirs[numTrails - 1];
            angDist = trailTargetAngs[0] - trailTargetAngs[numTrails - 1] - 180;
            for (var i = numTrails - 1; i > 0; --i)
            {
                trailDirs[i] = trailTargetDirs[i];
                angDist = Math.Max(angDist, trailTargetAngs[i] - trailTargetAngs[i - 1]);
                trailTargetDirs[i] = trailTargetDirs[i - 1];
            }

            trailDirs[0] = trailTargetDirs[0];
            trailTargetDirs[0] = tmp;

            time = 0.4f * castTime;
            angDist = angDist / time * Time.fixedDeltaTime;
            while (time > 0f)
            {
                for (var i = 0; i < numTrails; ++i)
                {
                    if ((trailDirs[i] - trailTargetDirs[i]).sqrMagnitude <= minSqrDist) continue;

                    trailDirs[i] = Quaternion.AngleAxis(-angDist, Vector3.up) * trailDirs[i];

#if UNITY_EDITOR
                    Debug.DrawRay(enemy.rigidbody.position,
                        trailDirs[i] * 100f,
                        Color.red,
                        4f,
                        false);
#endif
                    if (Physics.Raycast(enemy.rigidbody.position,
                            trailDirs[i],
                            out var hit,
                            Mathf.Infinity,
                            layerMask,
                            QueryTriggerInteraction.Ignore) &&
                        (hit.point - trailLastHits[i]).sqrMagnitude > minSqrDist)
                    {
                        poolManager.StartGoo(hit.point, Quaternion.identity);
                        trailLastHits[i] = hit.point;
                        yield return null;
                    }
                }

                time -= Time.fixedDeltaTime;
                yield return null;
            }

            time = 0.1f * castTime;
            angDist = 3 * numTrails * Time.fixedDeltaTime / time;
            while (time > 0f)
            {
                for (var i = 0; i < angDist; ++i)
                {
                    trailDirs[0] = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-90f, 90f), 0f) *
                                   Vector3.forward;

#if UNITY_EDITOR
                    Debug.DrawRay(enemy.rigidbody.position,
                        trailDirs[0] * 100f,
                        Color.red,
                        0.1f,
                        false);
#endif
                    if (Physics.Raycast(enemy.rigidbody.position,
                            trailDirs[0],
                            out var hit,
                            Mathf.Infinity,
                            layerMask,
                            QueryTriggerInteraction.Ignore))
                    {
                        poolManager.StartGoo(hit.point, Quaternion.identity);
                        yield return null;
                    }
                }

                time -= Time.fixedDeltaTime;
                yield return null;
            }
        }
    }
}
