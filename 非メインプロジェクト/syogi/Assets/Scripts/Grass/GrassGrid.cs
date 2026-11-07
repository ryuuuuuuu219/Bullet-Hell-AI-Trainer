using System;
using UnityEngine;

// Pure simulation: every cell reads the same previous generation.
public sealed class GrassGrid
{
    public const float InitialAmount = 1f;
    private float[] current;
    private float[] next;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float this[int x, int y] { get { return current[y * Width + x]; } }

    public GrassGrid(int width, int height, int initialCount, int seed)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("width");
        Width = width;
        Height = height;
        current = new float[checked(width * height)];
        next = new float[current.Length];
        if (initialCount < 0 || initialCount > current.Length)
            throw new ArgumentOutOfRangeException("initialCount");
        var random = new System.Random(seed);
        var indices = new int[current.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        for (int i = 0; i < initialCount; i++)
        {
            int j = random.Next(i, indices.Length);
            int index = indices[j];
            indices[j] = indices[i];
            indices[i] = index;
            current[index] = InitialAmount;
        }
    }

    // Centered world XY field. Lower edges are inclusive; upper edges exclusive.
    public bool TryWorldToCell(double worldX, double worldY, double cellSize, out int x, out int y)
    {
        x = y = -1;
        if (double.IsNaN(cellSize) || double.IsInfinity(cellSize) || cellSize <= 0)
            return false;
        double halfWidth = Width * cellSize / 2;
        double halfHeight = Height * cellSize / 2;
        if (!(worldX >= -halfWidth && worldX < halfWidth
            && worldY >= -halfHeight && worldY < halfHeight)) return false;
        x = Math.Min(Width - 1, (int)Math.Floor((worldX + halfWidth) / cellSize));
        y = Math.Min(Height - 1, (int)Math.Floor((worldY + halfHeight) / cellSize));
        return true;
    }

    // Returns the actual amount consumed, including zero for empty/outside cells.
    public float ConsumeAtWorldPosition(double worldX, double worldY, double cellSize, float requestedAmount)
    {
        int x, y;
        if (float.IsNaN(requestedAmount) || float.IsInfinity(requestedAmount) || requestedAmount <= 0 || !TryWorldToCell(worldX, worldY, cellSize, out x, out y)) return 0;
        int index = y * Width + x;
        if (current[index] <= 0) return 0;
        float consumed = Math.Min(current[index], requestedAmount);
        float remaining = current[index] - consumed;
        // Remove subtraction roundoff (e.g. five meals of 0.2 from 1).
        // Relative tolerance preserves genuinely small resource quantities.
        if (remaining <= Math.Max(current[index], requestedAmount) * 0.000001f)
        {
            consumed = current[index];
            remaining = 0f;
        }
        current[index] = remaining;
        return consumed;
    }

    public void Step(float maximum, float increase, int birthNeighborMinimum, bool growthEnabled, bool wrap, int birthNeighborMaximum = 1, float deathAmount = 0f)
    {
        if (float.IsNaN(maximum) || float.IsInfinity(maximum) || float.IsNaN(increase) || float.IsInfinity(increase)
            || float.IsNaN(deathAmount) || float.IsInfinity(deathAmount) || deathAmount > 0
            || maximum < 1 || increase < 0 || birthNeighborMinimum < 0 || birthNeighborMinimum > 8
            || birthNeighborMaximum < birthNeighborMinimum || birthNeighborMaximum > 8)
            throw new ArgumentOutOfRangeException("rule");
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int index = y * Width + x;
            float amount = current[index];
            if (amount < 0) next[index] = Math.Min(0f, amount + 1f);
            else if (amount == 0)
            {
                int neighbors = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (wrap)
                    {
                        nx = (nx + Width) % Width;
                        ny = (ny + Height) % Height;
                    }
                    else if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;
                    if (current[ny * Width + nx] > 0) neighbors++;
                }
                // Empty cells can also sprout spontaneously with a 0.1% chance per tick.
                next[index] = (neighbors >= birthNeighborMinimum && neighbors <= birthNeighborMaximum) ||
                    UnityEngine.Random.Range(0f, 1f) < 0.001f
                    ? InitialAmount : 0;
            }
            // A mature cell dies on this tick; it cannot also be reborn on this tick.
            else if (amount >= maximum) next[index] = deathAmount;
            else next[index] = Math.Min(maximum, amount + (growthEnabled ? increase : 0));
        }
        var previous = current;
        current = next;
        next = previous;
    }
}
