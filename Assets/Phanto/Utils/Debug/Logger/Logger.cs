// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace PhantoUtils
{
    public static class Logger
    {
        public enum Severity
        {
            Verbose = 0,
            Low,
            Moderate,
            Severe
        }

        public enum Type
        {
            General = 0,
            Error,
            Performance
        }

        public enum TypeMask
        {
            None = 0x0,
            General = 0x1,
            Error = 0x2,
            Performance = 0x4,
            All = 0x7
        }

        public static Severity minimumSeverity = Severity.Verbose;
        public static TypeMask enabledMessageTypes = TypeMask.All;

        private static readonly LogType[,] unityLogType =
        {
            { LogType.Log, LogType.Log, LogType.Log, LogType.Log },
            { LogType.Warning, LogType.Warning, LogType.Warning, LogType.Error },
            { LogType.Warning, LogType.Warning, LogType.Warning, LogType.Error }
        };

        [Conditional("UNITY_ASSERTIONS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Assert(bool condition,
            string msg = "",
            Object context = null,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            //alexdaws: don't short-circuit the & for efficiency
            if (!condition &
                (((1 << (int)Type.Error) & (int)enabledMessageTypes) > 0) &
                (Severity.Severe >= minimumSeverity))
                Debug.LogFormat(LogType.Assert,
                    LogOption.NoStacktrace,
                    context,
                    $"[{Enum.GetName(typeof(Type), Type.Error)}][{Enum.GetName(typeof(Severity), Severity.Severe)}] {Assembly.GetCallingAssembly().GetName().Name}: Assert Failed! \"{msg}\" from {callerName} in <a href=\"{filePath}\" line=\"{lineNumber}\">{filePath}:{lineNumber}</a>\n{new StackTrace(1)}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(Type msgType,
            Severity msgSeverity,
            string msg,
            Object context = null,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            //alexdaws: don't short-circuit the & for efficiency
            if ((((1 << (int)msgType) & (int)enabledMessageTypes) > 0) &
                (msgSeverity >= minimumSeverity))
            {
                if (msgType == Type.Error)
                    Debug.LogFormat(unityLogType[(int)msgType, (int)msgSeverity],
                        LogOption.NoStacktrace,
                        context,
                        $"[{Enum.GetName(typeof(Type), msgType)}][{Enum.GetName(typeof(Severity), msgSeverity)}] {Assembly.GetCallingAssembly().GetName().Name}: \"{msg}\" from {callerName} in <a href=\"{filePath}\" line=\"{lineNumber}\">{filePath}:{lineNumber}</a>\n{new StackTrace(1)}");
                else
                    Debug.LogFormat(unityLogType[(int)msgType, (int)msgSeverity],
                        LogOption.NoStacktrace,
                        context,
                        $"[{Enum.GetName(typeof(Type), msgType)}][{Enum.GetName(typeof(Severity), msgSeverity)}] {Assembly.GetCallingAssembly().GetName().Name}: \"{msg}\" from {callerName} in <a href=\"{filePath}\" line=\"{lineNumber}\">{filePath}:{lineNumber}</a>");
            }
        }
    }
}
