using System;
using System.Collections.Generic;

// Centers occupy one cell each. Queries are conservative; callers test exact geometry.
public sealed class SpatialGrid<T>
{
    private readonly double cellSize;
    private readonly Dictionary<(int, int), HashSet<T>> cells = new Dictionary<(int, int), HashSet<T>>();
    private readonly Dictionary<T, (int, int)> locations = new Dictionary<T, (int, int)>();

    public SpatialGrid(double cellSize)
    {
        if (double.IsNaN(cellSize) || double.IsInfinity(cellSize) || cellSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        this.cellSize = cellSize;
    }

    private int Cell(double coordinate) => (int)Math.Floor(coordinate / cellSize);

    public void Update(T item, double x, double y)
    {
        var key = (Cell(x), Cell(y));
        if (locations.TryGetValue(item, out var previous) && previous == key) return;
        Remove(item);
        if (!cells.TryGetValue(key, out var bucket))
            cells.Add(key, bucket = new HashSet<T>());
        bucket.Add(item);
        locations[item] = key;
    }

    public void Remove(T item)
    {
        if (!locations.TryGetValue(item, out var key)) return;
        var bucket = cells[key];
        bucket.Remove(item);
        if (bucket.Count == 0) cells.Remove(key);
        locations.Remove(item);
    }

    public void Query(double minX, double minY, double maxX, double maxY, List<T> results)
    {
        results.Clear();
        for (int y = Cell(minY); y <= Cell(maxY); y++)
        for (int x = Cell(minX); x <= Cell(maxX); x++)
            if (cells.TryGetValue((x, y), out var bucket)) results.AddRange(bucket);
    }

    public void Clear() { cells.Clear(); locations.Clear(); }
}
