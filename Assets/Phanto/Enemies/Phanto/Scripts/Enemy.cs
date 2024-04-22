// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Phanto
{
    /// <summary>
    /// Enemy behaviour
    /// </summary>
    [DefaultExecutionOrder(-1)]
    public class Enemy : MonoBehaviour, IDamageable
    {
        private const double HOVER_NOISE_MAX_TIME = 3600.0;

        [SerializeField] private MovementSettings movementSettings = MovementSettings.DEFAULTS;

        [SerializeField] private MovementSettings defaultMovementSettings = MovementSettings.DEFAULTS;

        [SerializeField] private float dropYVel = 3f;

        [SerializeField] public EnemyProximitySensor proxSensor;

        [HideInInspector] public new Rigidbody rigidbody;

        [SerializeField] private float initialHealth = 100f;

        [SerializeField] public bool invulnerable;

        [SerializeField] public float painThreshold = 0.15f;

        private IDamageable.DamageCallback lastDamageCallback;

        public float maxVelocity
        {
            get => movementSettings.maxVelocity;
            set => movementSettings.maxVelocity = value;
        }

        public float maxAcceleration
        {
            get => movementSettings.maxAcceleration;
            set => movementSettings.maxAcceleration = value;
        }

        public Vector2 easingDists
        {
            get => movementSettings.easingDists;
            set => movementSettings.easingDists = value;
        }

        public float maxAngularVelocity
        {
            get => movementSettings.maxAngularVelocity;
            set => movementSettings.maxAngularVelocity = value;
        }

        public float maxAngularAcceleration
        {
            get => movementSettings.maxAngularAcceleration;
            set => movementSettings.maxAngularAcceleration = value;
        }

        public Vector2 easingAngularDists
        {
            get => movementSettings.easingAngularDists;
            set => movementSettings.easingAngularDists = value;
        }

        public float hoverRadius
        {
            get => movementSettings.hoverRadius;
            set => movementSettings.hoverRadius = value;
        }

        public float hoverNoiseOffset
        {
            get => movementSettings.hoverNoiseOffset;
            set => movementSettings.hoverNoiseOffset = value;
        }

        public Vector3 hoverNoiseFrequency
        {
            get => movementSettings.hoverNoiseFrequency;
            set => movementSettings.hoverNoiseFrequency = value;
        }

        public float Health { get; private set; } = 100f;

        private void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            Init();
        }

        public void Heal(float healing, IDamageable.DamageCallback callback = null)
        {
            Health += healing;
        }

        public void TakeDamage(float damage, Vector3 position, Vector3 normal, GameObject damageSource = null,
            IDamageable.DamageCallback callback = null)
        {
            if (invulnerable) return;

            Health -= damage;

            if (callback != null)
            {
                lastDamageCallback = callback;

                if (Health < 0f) damage += Health;
                if (damage > 0f) callback(this, damage, Health <= 0f);
            }
        }

        public static bool InView(Transform eye, Transform target)
        {
            var dir = (target.position - eye.position).normalized;
            dir = Quaternion.Inverse(eye.rotation) * dir;
            if (dir.z <= 0f) return false;

            dir /= dir.z;
            if (Mathf.Abs(dir.x) > 1f ||
                Mathf.Abs(dir.y) > 1f)
                return false;

            return true;
        }

        public void ResetMovementSettings()
        {
            movementSettings = defaultMovementSettings;
        }

        public void UpdateMovementDefaults()
        {
            defaultMovementSettings = movementSettings;
        }

        private float GenerateHoverNoise(HoverComponent comp, float freqScale = 1f)
        {
            var x = (float)((Time.timeAsDouble + hoverNoiseOffset) % HOVER_NOISE_MAX_TIME) * freqScale *
                    hoverNoiseFrequency[(int)comp];
            var y = (float)comp * 10f + hoverNoiseOffset;
            return 2f * Mathf.Clamp01(Mathf.PerlinNoise(x, y)) - 1f;
        }

        private float GenerateHoverSin(HoverComponent comp, float freqScale = 1f)
        {
            var x = (float)((Time.timeAsDouble + hoverNoiseOffset) % HOVER_NOISE_MAX_TIME) * freqScale *
                    (hoverNoiseFrequency[(int)comp] * 0.4f + 0.6f);
            var y = (float)comp * 10f + hoverNoiseOffset;
            return Mathf.Sin(x + y);
        }

        private void RandomizeHover()
        {
            defaultMovementSettings.hoverNoiseOffset = Random.Range(-3600f, 3600f);
            defaultMovementSettings.hoverNoiseFrequency = Vector3.Scale(new Vector3(0.9f * Random.value + 0.1f,
                    0.9f * Random.value + 0.1f,
                    0.9f * Random.value + 0.1f),
                hoverNoiseFrequency);

            hoverNoiseOffset = defaultMovementSettings.hoverNoiseOffset;
            hoverNoiseFrequency = defaultMovementSettings.hoverNoiseFrequency;
        }

        public bool FlyTo(Vector3 target)
        {
            var targetVel = target - rigidbody.position;
            targetVel = Vector3.Lerp(Vector3.zero,
                targetVel.normalized * maxVelocity,
                easingDists.y * (Mathf.Sqrt(targetVel.magnitude) - easingDists.x));
            var reachedTarget = targetVel.sqrMagnitude <= 0.1f;
            targetVel -= rigidbody.velocity;

            var accel = targetVel / Time.fixedDeltaTime;
            if (accel.sqrMagnitude > maxAcceleration * maxAcceleration) accel = accel.normalized * maxAcceleration;
            accel -= Physics.gravity;

            rigidbody.AddForce(accel,
                ForceMode.Acceleration);
            return reachedTarget;
        }

        public void HoverAround(Vector3 target)
        {
            var noise = Vector3.zero;
            if (hoverRadius != 0f)
                noise = hoverRadius * new Vector3(GenerateHoverNoise(HoverComponent.X),
                    GenerateHoverNoise(HoverComponent.Y),
                    GenerateHoverNoise(HoverComponent.Z));

            FlyTo(target + noise);
        }

        public void CircleStrafe(Vector3 circleCenter, bool clockwise)
        {
            var targetVel = rigidbody.position - circleCenter;
            var circleRadius = targetVel.magnitude;
            targetVel = Vector3.ProjectOnPlane(targetVel, Vector3.up).normalized;
            targetVel = Vector3.Cross(targetVel, Vector3.up);
            targetVel = (clockwise ? maxVelocity : -maxVelocity) * targetVel;

            var closeVel = rigidbody.position + Time.fixedDeltaTime * targetVel;
            closeVel -= circleCenter;
            closeVel = (closeVel.magnitude - circleRadius) * closeVel.normalized;
            targetVel += closeVel;

            var hoverNoise = Vector3.up;
            if (hoverRadius != 0f) hoverNoise *= 0.5f * hoverRadius * GenerateHoverSin(HoverComponent.Y, 2f * Mathf.PI);
            targetVel += hoverNoise;
            targetVel -= rigidbody.velocity;

            var accel = targetVel / Time.fixedDeltaTime;
            if (accel.sqrMagnitude > maxAcceleration * maxAcceleration) accel = accel.normalized * maxAcceleration;
            accel -= Physics.gravity;

            rigidbody.AddForce(accel, ForceMode.Acceleration);
        }

        public void MoveAlong(Vector3 dir)
        {
            var targetVel = maxVelocity * dir;

            var hoverNoise = Vector3.up;
            if (hoverRadius != 0f) hoverNoise *= 0.5f * hoverRadius * GenerateHoverSin(HoverComponent.Y, 2f * Mathf.PI);
            targetVel += hoverNoise;
            targetVel -= rigidbody.velocity;

            var accel = targetVel / Time.fixedDeltaTime;
            if (accel.sqrMagnitude > maxAcceleration * maxAcceleration) accel = accel.normalized * maxAcceleration;
            accel -= Physics.gravity;

            rigidbody.AddForce(accel, ForceMode.Acceleration);
        }

        public bool AimAlong(Vector3 dir)
        {
            var rot = rigidbody.rotation * Vector3.forward;
            Debug.DrawRay(rigidbody.position, rot, Color.blue);

            var angularVel = Vector3.Angle(rot, dir);
            angularVel = Mathf.Lerp(0f,
                maxAngularVelocity,
                easingAngularDists.y * (angularVel - easingAngularDists.x));
            rot = Vector3.Cross(rot, dir).normalized * angularVel;
            rot -= rigidbody.angularVelocity;

            rot /= Time.fixedDeltaTime;
            if (rot.sqrMagnitude > maxAngularAcceleration * maxAngularAcceleration)
                rot = rot.normalized * maxAngularAcceleration;
            rigidbody.AddTorque(rot, ForceMode.Acceleration);

            return angularVel == 0f;
        }

        public bool AimTowards(Vector3 target)
        {
            return AimAlong((target - rigidbody.position).normalized);
        }

        public bool AimAway(Vector3 target)
        {
            return AimAlong((rigidbody.position - target).normalized);
        }

        public bool KeepUpright(Vector3 up)
        {
            var rot = rigidbody.rotation * Vector3.up;
            Debug.DrawRay(rigidbody.position, rot, Color.green);

            var forward = rigidbody.rotation * Vector3.forward;
            up = Vector3.ProjectOnPlane(up, forward);
            Debug.DrawRay(rigidbody.position, up, Color.magenta);

            var angularVel = Vector3.Angle(rot, up);
            angularVel = Mathf.Lerp(0f,
                maxAngularVelocity,
                easingAngularDists.y * (angularVel - easingAngularDists.x));
            rot = Vector3.Cross(rot, up).normalized * angularVel;
            rot /= Time.fixedDeltaTime;
            rigidbody.AddTorque(rot, ForceMode.Acceleration);

            return angularVel == 0f;
        }

        public void BarrelRoll(bool clockwise)
        {
            var rot = rigidbody.rotation * Vector3.forward;
            rot *= clockwise ? maxAngularVelocity : -maxAngularVelocity;
            rot /= Time.fixedDeltaTime;

            rigidbody.AddTorque(rot, ForceMode.Acceleration);
        }

        public void Spin(bool clockwise)
        {
            var rot = rigidbody.rotation * Vector3.up;
            rot *= clockwise ? maxAngularVelocity : -maxAngularVelocity;
            rot /= Time.fixedDeltaTime;

            rigidbody.AddTorque(rot, ForceMode.Acceleration);
        }

        public void DestroySelf()
        {
            PoolManagerSingleton.Instance.Discard(gameObject);
        }

        public void Init()
        {
            Health = initialHealth;
            ResetMovementSettings();
            RandomizeHover();
        }

        public void ResetHealth()
        {
            Health = initialHealth;
        }

        [Serializable]
        public struct Drop
        {
            public float chance;
            public int numRolls;
            public int numToDrop;
            public GameObject dropPrefab;
        }

        [Serializable]
        public struct MovementSettings
        {
            public static readonly MovementSettings DEFAULTS =
                new()
                {
                    maxVelocity = 6f,
                    maxAcceleration = 15f,
                    easingDists = new Vector2(0.01f, 1f / (2f - 0.01f)),
                    maxAngularVelocity = 15f,
                    maxAngularAcceleration = 60f,
                    easingAngularDists = new Vector2(0.5f, 1f / (20f - 0.5f)),
                    hoverRadius = 0.8f,
                    hoverNoiseOffset = 0f,
                    hoverNoiseFrequency = new Vector3(0f, 1f, 0f)
                };

            public float maxVelocity;
            public float maxAcceleration;
            public Vector2 easingDists;
            public float maxAngularVelocity;
            public float maxAngularAcceleration;
            public Vector2 easingAngularDists;
            public float hoverRadius;
            public float hoverNoiseOffset;
            public Vector3 hoverNoiseFrequency;
        }

        private enum HoverComponent
        {
            X,
            Y,
            Z
        }
    }
}
