// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = PhantoUtils.Logger;
using Random = UnityEngine.Random;

namespace Phanto
{
    /// <summary>
    ///     Represents a player in the game.
    /// </summary>
    public class Player : MonoBehaviour, IDamageable
    {
        public delegate void GameOverHandler(bool victory);

        public const int MAX_PLAYERS = 8;
        public static bool gameOver = false;
        public static int numPlayers;
        public static int playersLeft;
        private static readonly Player[] players = new Player[8];
        public static Player localPlayer = null;

        private static readonly Vector3[] detectionOffsets =
        {
            Vector3.zero,
            new(0.99f,
                0.49f,
                0.99f),
            new(-0.99f,
                0.49f,
                -0.99f)
        };

        [NonSerialized] private CapsuleCollider capsuleCollider;

        [HideInInspector] public Stats stats;

        public static IReadOnlyList<Player> Players => players.Take(numPlayers).ToList();
        public int PlayerUID { get; private set; } = -1;

        private void Start()
        {
            PlayerUID = numPlayers;
            players[PlayerUID] = this;
            ++numPlayers;
            ++playersLeft;
            Logger.Log(Logger.Type.General, Logger.Severity.Verbose,
                "Player UID: " + PlayerUID + " Players Left: " + playersLeft + " numPlayers: " + numPlayers, this);

            if (Phanto.Instance != null) Phanto.Instance.OnWaveAdvance += OnWaveAdvance;

            capsuleCollider = GetComponent<CapsuleCollider>();
        }

        private void FixedUpdate()
        {
            if (!gameOver) ++stats.ticksSurvived;
        }

        public void Heal(float healing, IDamageable.DamageCallback callback = null)
        {
        }

        /// <summary>
        ///     Called when a player dies.
        /// </summary>
        public void TakeDamage(float damage, Vector3 position, Vector3 normal,
            IDamageable.DamageCallback callback = null)
        {
        }

        public static void ClearPlayers()
        {
            Array.Clear(players, 0, MAX_PLAYERS);
            numPlayers = 0;
            playersLeft = 0;
        }

        /// <summary>
        ///     Gets the player with the given ID.
        /// </summary>
        public static Player GetClosestLivePlayer(Vector3 position)
        {
            if (playersLeft <= 0) return null;

            var closest = players[0];
            var closestDist = (position - closest.transform.position).sqrMagnitude;
            for (var i = 1; i < playersLeft; ++i)
            {
                var dist = (position - players[i].transform.position).sqrMagnitude;
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = players[i];
                }
            }

            return closest;
        }

        public static Player GetRandomLivePlayer()
        {
            if (playersLeft <= 0) return null;

            return players[Random.Range(0, playersLeft)];
        }

        public bool IsDetectable(Transform eye)
        {
            foreach (var offset in detectionOffsets)
            {
                var dir = Vector3.Scale(offset, new Vector3(capsuleCollider.radius,
                    capsuleCollider.height,
                    capsuleCollider.radius));
                dir += capsuleCollider.center;
                dir = (transform.TransformPoint(dir) - eye.position).normalized;
                var hits = Physics.RaycastAll(eye.position,
                    dir,
                    Mathf.Infinity,
                    LayerMask.GetMask("OVRScene", "Player"),
                    QueryTriggerInteraction.Ignore);
                if (hits.Length <= 0) continue;

                var closestHit = hits[0];
                for (var i = 1; i < hits.Length; ++i)
                {
                    if (hits[i].transform.gameObject != gameObject &&
                        hits[i].transform.gameObject.layer == LayerMask.NameToLayer("Player"))
                        continue;

                    if (hits[i].distance < closestHit.distance) closestHit = hits[i];
                }

                if (closestHit.transform.gameObject == gameObject) return true;
            }

            return false;
        }

        public void TrackDamageStats(IDamageable damagableAffected, float hpAffected, bool targetDied)
        {
            if (gameOver ||
                !(damagableAffected is Enemy))
                return;

            ++stats.shotsHit;
            stats.damageDealt += hpAffected;
            stats.enemiesKilled += targetDied ? 1u : 0u;

            var dmg = (uint)Mathf.Ceil(hpAffected);
            stats.score += 10u * dmg + (targetDied ? 1000u : 0u);
        }

        public void OnWeaponFired(Vector3 shotOrigin,
            Vector3 shotDirection,
            uint numProjectiles)
        {
            if (gameOver) return;

            stats.shotsFired += numProjectiles;
        }

        public void OnWaveAdvance()
        {
            ++stats.wavesSurvived;
        }

        public void DebugPrintStats()
        {
            Logger.Log(Logger.Type.General,
                Logger.Severity.Verbose,
                "Player " + (PlayerUID + 1) + " Score: " + players[PlayerUID].stats.score + "\n" +
                " Waves Survived: " + players[PlayerUID].stats.wavesSurvived + "\n" +
                " Kills: " + players[PlayerUID].stats.enemiesKilled + "\n" +
                " Damage Dealt: " + Mathf.Ceil(players[PlayerUID].stats.damageDealt) + "\n" +
                " Shots Fired: " + players[PlayerUID].stats.shotsFired + "\n" +
                " Accuracy: " + players[PlayerUID].stats.CalculateAccuracy().ToString("P2") + "\n" +
                " Time Survived: " +
                (players[PlayerUID].stats.ticksSurvived * (double)Time.fixedDeltaTime).ToString("0.000"),
                this);
        }

        public struct Stats
        {
            public uint score,
                wavesSurvived,
                shotsFired,
                shotsHit,
                enemiesKilled;

            public float damageDealt;
            public ulong ticksSurvived;

            public double CalculateAccuracy()
            {
                return shotsFired == 0 ? 0 : shotsHit / (double)shotsFired;
            }
        }
    }
}
