// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

/// <summary>
/// NavMeshConstants is a class containing static properties for NavMesh related
/// calculations.
public static class NavMeshConstants
{
    private const string SceneMesh = "GlobalMesh";
    private const string Default = "Default";

    // Approximate radius of a tennis ball, used for sphere casting.
    public const float TennisBall = 0.0325f;
    public const float OneFoot = 0.3048f;

    public const int Walkable = 0;
    public const int NotWalkable = 1;
    public const int JumpArea = 2;
    public const int FloorArea = 3;
    public const int FurnitureArea = 4;

    public const int JumpAreaMask = 1 << JumpArea;
    public const int FloorAreaMask = 1 << FloorArea;
    public const int FurnitureAreaMask = 1 << FurnitureArea;
    public static int SceneMeshLayerMask { get; private set; }
    public static int DefaultLayerMask { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void Initialize()
    {
        SceneMeshLayerMask = LayerMask.GetMask(SceneMesh);
        DefaultLayerMask = LayerMask.GetMask(Default);
    }
}
