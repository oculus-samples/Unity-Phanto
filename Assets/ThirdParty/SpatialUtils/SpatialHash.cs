// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using UnityEngine;

/// <summary>
///
/// </summary>
/// <typeparam name="T"></typeparam>
public class SpatialHash<T> : SpatialBase<T>
{
    private readonly Dictionary<HashCell, HashSet<T>> _hashTable = new Dictionary<HashCell, HashSet<T>>();

    public override int Count => _hashTable.Count;
    public override IReadOnlyCollection<HashCell> Cells => _hashTable.Keys;

    public SpatialHash(float cellSize) : base(cellSize)
    {
    }

    /// <summary>
    /// Adds an item to the spatial hash
    /// </summary>
    /// <param name="position"></param>
    /// <param name="item"></param>
    public override void Add(Vector3 position, T item)
    {
        var cell = HashCell.GetCell(position, cellSize);
        if (!_hashTable.ContainsKey(cell))
        {
            _hashTable[cell] = new HashSet<T>();
        }
        _hashTable[cell].Add(item);
    }

    /// <summary>
    /// Attempts to remove an item from the spatial hash
    /// </summary>
    /// <param name="position"></param>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool Remove(Vector3 position, T item)
    {
        var cell = HashCell.GetCell(position, cellSize);

        if (!_hashTable.TryGetValue(cell, out var hashSet))
        {
            return false;
        }

        if (!hashSet.Remove(item))
        {
            return false;
        }

        _hashTable[cell] = hashSet;
        return true;
    }

    public bool RemoveCell(HashCell cell)
    {
        return _hashTable.Remove(cell, out var _);
    }

    public bool Remove(T item)
    {
        var found = false;

        foreach (var hashSet in _hashTable.Values)
        {
            found |= hashSet.Remove(item);
        }

        return found;
    }

    /// <summary>
    /// Attempts to empty the cell that contains point.
    /// </summary>
    /// <param name="cell"></param>
    /// <returns></returns>
    public override bool Clear(HashCell cell)
    {
        if (!_hashTable.TryGetValue(cell, out var points))
        {
            return false;
        }

        points.Clear();
        _hashTable[cell] = points;
        return true;
    }

    public override bool TryGetCellContents(HashCell cell, out IReadOnlyCollection<T> items)
    {
        if (!_hashTable.TryGetValue(cell, out var result))
        {
            items = null;
            return false;
        }

        items = result;
        return true;
    }
}
