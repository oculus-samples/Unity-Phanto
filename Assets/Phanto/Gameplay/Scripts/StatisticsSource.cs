// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for all statistics sources
/// </summary>
[MetaCodeSample("Phanto")]
public abstract class StatisticsSource : MonoBehaviour
{
    public abstract List<Statistics> StatisticsList();
    public abstract bool IsReady();
}
