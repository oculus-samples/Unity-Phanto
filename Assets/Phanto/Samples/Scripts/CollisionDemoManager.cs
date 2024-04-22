// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using System.Diagnostics;
using Phanto.Enemies.DebugScripts;
using Phantom.Environment.Scripts;
using PhantoUtils;
using UnityEngine;
using Utilities.XR;

namespace Phantom
{
    public class CollisionDemoManager : MonoBehaviour, ICollisionDemo
    {
        [SerializeField] protected Transform leftHand;
        [SerializeField] protected Transform rightHand;

        [SerializeField] protected bool debugDraw = true;

        [SerializeField] protected BouncingPhantomController phantomPrefab;
        [SerializeField] protected DemoGoo gooPrefab;

        [SerializeField] protected int spawnCount = 16;

        private readonly List<ContactPoint> _contactPoints = new(256);
        private readonly Queue<(Vector3 pos, Vector3 normal, long ms)> _pointQueue = new();
        private readonly Stopwatch _queueTimer = Stopwatch.StartNew();
        private readonly Queue<DemoGoo> _gooPool = new();

        private readonly Queue<BouncingPhantomController> _phantomPool = new();

        private bool _sceneReady;

        private bool _started;

        private void Awake()
        {
            DebugDrawManager.DebugDraw = debugDraw;
        }

        private void Update()
        {
            if (!_sceneReady || !_started)
            {
                return;
            }

            XRGizmos.DrawPointer(leftHand.position, leftHand.forward, Color.blue, 0.1f);
            XRGizmos.DrawPointer(rightHand.position, rightHand.forward, Color.red, 0.1f);

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
            {
                var ray = new Ray(leftHand.position, leftHand.forward);

                ThrowPhantom(ray);
            }

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                var ray = new Ray(rightHand.position, rightHand.forward);

                ThrowPhantom(ray);
            }
        }

        private void OnEnable()
        {
            SceneBoundsChecker.BoundsChanged += OnBoundsChanged;
            DebugDrawManager.DebugDrawEvent += DebugDraw;
        }

        private void OnDisable()
        {
            SceneBoundsChecker.BoundsChanged -= OnBoundsChanged;
            DebugDrawManager.DebugDrawEvent -= DebugDraw;
        }

        private void Start()
        {
            for (var i = 0; i < spawnCount; i++)
            {
                var phantom = Instantiate(phantomPrefab, Vector3.zero,
                    Quaternion.identity);
                phantom.Initialize(this);
                phantom.Hide();
                _phantomPool.Enqueue(phantom);
            }

            for (var i = 0; i < spawnCount * 8; i++)
            {
                var goo = Instantiate(gooPrefab, Vector3.zero,
                    Quaternion.identity);
                goo.Hide();
                _gooPool.Enqueue(goo);
            }

            _started = true;
        }

        public void RenderCollision(Collision collision)
        {
            var count = collision.GetContacts(_contactPoints);

            for (var i = 0; i < count; i++)
            {
                var contact = _contactPoints[i];
                _pointQueue.Enqueue((contact.point, contact.normal, _queueTimer.ElapsedMilliseconds));

                // a single collision can have multiple points very close together.
                if (i > 0 && Vector3.Distance(contact.point, _contactPoints[i - 1].point) < 0.02f) continue;

                if (_gooPool.TryDequeue(out var goo))
                {
                    goo.SpawnAt(contact.point, contact.normal);
                    _gooPool.Enqueue(goo);
                }
            }
        }

        private void ThrowPhantom(Ray ray)
        {
            if (!_phantomPool.TryDequeue(out var phantom)) return;

            phantom.Launch(ray);

            _phantomPool.Enqueue(phantom);
        }

        private void OnBoundsChanged(Bounds bounds)
        {
            _sceneReady = true;
        }

        private void DebugDraw()
        {
            const long duration = 5000;
            var currentMs = _queueTimer.ElapsedMilliseconds;

            while (_pointQueue.TryPeek(out var peek))
            {
                if (peek.ms > currentMs - duration) break;

                _pointQueue.Dequeue();
            }

            foreach (var p in _pointQueue)
            {
                var position = p.pos;
                var rotation = Quaternion.FromToRotation(Vector3.up, p.normal);

                XRGizmos.DrawCircle(position, rotation, 0.05f, MSPalette.BlueViolet);
                XRGizmos.DrawPointer(position, p.normal, MSPalette.Goldenrod, 0.05f, 0.005f);
            }
        }
    }
}
