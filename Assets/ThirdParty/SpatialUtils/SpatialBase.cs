// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public abstract class SpatialBase<T>
{
    protected readonly float cellSize;

    public abstract int Count { get; }
    public float CellSize => cellSize;
    public abstract IReadOnlyCollection<HashCell> Cells { get; }

    // public SpatialBase() : this(0.5f)
    // {
    // }

    protected SpatialBase(float cellSize)
    {
        Assert.IsTrue(cellSize > 0.0f);
        this.cellSize = cellSize;
    }

    /// <summary>
    /// Adds an item to the spatial structure
    /// </summary>
    /// <param name="position"></param>
    /// <param name="item"></param>
    public abstract void Add(Vector3 position, T item);

    /// <summary>
    /// Attempts to empty the cell that contains point.
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public bool Clear(Vector3 position)
    {
        var cell = HashCell.GetCell(position, cellSize);

        return Clear(cell);
    }

    public abstract bool Clear(HashCell cell);

    /// <summary>
    /// Get a collection of points that share a cell with position.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="items"></param>
    /// <returns></returns>
    public bool TryGetCell(Vector3 position, out IReadOnlyCollection<T> items)
    {
        var cell = HashCell.GetCell(position, cellSize);

        return TryGetCellContents(cell, out items);
    }

    public abstract bool TryGetCellContents(HashCell cell, out IReadOnlyCollection<T> items);

    public Bounds GetCellBounds(Vector3 position)
    {
        (int x, int y, int z) = HashCell.GetCellCoords(position, cellSize);

        var cellPosition = new Vector3(x, y, z) * cellSize;
        var size = Vector3.one * cellSize;
        var bounds = new Bounds(cellPosition, size);

        return bounds;
    }

    public Vector3 CellToWorld(in HashCell cell)
    {
        return new Vector3(cell.x * cellSize, cell.y * cellSize, cell.z * cellSize);
    }

    /// <summary>
    /// Get list of points that are within the nine cells near position
    /// </summary>
    /// <param name="position"></param>
    /// <param name="nearbyItems"></param>
    /// <returns></returns>
    public int GetNearby(Vector3 position, HashSet<T> nearbyItems)
    {
        var cell = HashCell.GetCell(position, cellSize);
        nearbyItems.Clear();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    var neighborIndex = HashCell.GetCell(cell.x + x, cell.y + y, cell.z + z);
                    if (TryGetCellContents(neighborIndex, out var values))
                    {
                        foreach (var item in values)
                        {
                            nearbyItems.Add(item);
                        }
                    }
                }
            }
        }

        return nearbyItems.Count;
    }
}
