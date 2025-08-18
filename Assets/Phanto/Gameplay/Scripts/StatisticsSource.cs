// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for all statistics sources
/// </summary>
public abstract class StatisticsSource : MonoBehaviour
{
    public abstract List<Statistics> StatisticsList();
    public abstract bool IsReady();
}
