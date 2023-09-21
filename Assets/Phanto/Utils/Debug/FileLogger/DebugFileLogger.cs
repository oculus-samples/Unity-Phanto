// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace PhantoUtils
{
    /**
     * <summary>
     *     A component to save application level logs to file. Include this in the scene to start capturing logs.
     * </summary>
     * <remarks>
     *     Logs before scene load will not be captured, and logs during shutdown may not be captured depending on script
     *     execution timing.
     * </remarks>
     */
    [DefaultExecutionOrder(-10000)] // Set the execution order very early to catch as many startup logs as possible
    public class DebugFileLogger : MonoBehaviour
    {
        private static readonly int InitialLogBufferBytes = 4096;

        [SerializeField] private bool logInEditor;
        private Task _activeLogTask;

        private bool _applicationQuitting;

        private FileStream _fileStream;
        private int _flushTickDelay;
        private byte[] _logByteBuffer = new byte[InitialLogBufferBytes];

        private string _logFilePath;
        private bool _streamDirty;

        private void Awake()
        {
            var productName = Regex.Replace(Application.productName, @"[^A-Za-z]+", string.Empty);
            var filename = $"{productName}_Logs_{GetDateAndTime()}.txt";
            _logFilePath = Path.Combine(Application.persistentDataPath, filename);
            _flushTickDelay = Mathf.Max(Application.targetFrameRate, 60) / 2;
        }

        private void Update()
        {
            if (!_streamDirty
                || (_activeLogTask != null && !_activeLogTask.IsCompleted)
                || Time.frameCount % _flushTickDelay != 0) // Only flush every n ticks
                return;

            _activeLogTask = _fileStream.FlushAsync();
        }

        private void OnEnable()
        {
            if (Application.isEditor && !logInEditor)
            {
                enabled = false;
                return;
            }

            _fileStream = File.Open(_logFilePath, FileMode.OpenOrCreate);
            Application.logMessageReceived += Log;
            Debug.Log(
                $"{nameof(DebugFileLogger)}: Writing logs to {_logFilePath}, max buffer size {_logByteBuffer.Length}.");
        }

        private void OnDisable()
        {
            if (_applicationQuitting) return;

            Application.logMessageReceived -= Log;
            CloseStream();
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
            Debug.Log($"{nameof(DebugFileLogger)}: Application quitting.");
        }

        private void Log(string message, string stacktrace, LogType type)
        {
            LogToFile(message);
            if (type == LogType.Exception) LogToFile(stacktrace);
        }

        private async void LogToFile(string message)
        {
            var logText = $"{GetTime()} {message}\n";

            if (_activeLogTask != null && !_activeLogTask.IsCompleted)
                // Await the active task since we use a single byte buffer, don't run multiple simultaneously
                await _activeLogTask;

            // Check that the max possible byte size fits in the allocated buffer. If it doesn't, check if the actual byte size fits, and if it doesn't either then reallocate.
            if (Encoding.Default.GetMaxByteCount(logText.Length) > _logByteBuffer.Length)
            {
                var byteCount = Encoding.Default.GetByteCount(logText);
                if (byteCount > _logByteBuffer.Length)
                {
                    Debug.Log($"{nameof(DebugFileLogger)}: Resizing buffer to {byteCount} bytes.");
                    _logByteBuffer = new byte[(int)(byteCount * 1.5f)];
                }
            }

            var numBytes = Encoding.Default.GetBytes(logText, 0, logText.Length, _logByteBuffer, 0);
            _activeLogTask = _fileStream.WriteAsync(_logByteBuffer, 0, numBytes);
            _streamDirty = true;

            // When quitting, write synchronously
            if (_applicationQuitting)
            {
                await _activeLogTask;
                _fileStream.Flush();
            }
        }

        private async void CloseStream()
        {
            if (_activeLogTask != null && _activeLogTask.Status == TaskStatus.Running) await _activeLogTask;

            if (_fileStream != null)
            {
                _fileStream.Flush();
                _fileStream.Dispose();
                _fileStream = null;
            }
        }

        private static string GetDateAndTime()
        {
            return DateTime.Now.ToString("yy-MM-dd_HH-mm-ss");
        }

        private static string GetTime()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff");
        }
    }
}
