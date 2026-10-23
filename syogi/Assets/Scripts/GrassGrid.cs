using System;

// Pure simulation: every cell reads the same previous generation.
public sealed class GrassGrid
{
    public const int InitialAmount = 1;
    private int[] current;
    private int[] next;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int this[int x, int y] { get { return current[y * Width + x]; } }

    public GrassGrid(int width, int height, int initialCount, int seed)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("width");
        Width = width;
        Height = height;
        current = new int[checked(width * height)];
        next = new int[current.Length];
        if (initialCount < 0 || initialCount > current.Length)
            throw new ArgumentOutOfRangeException("initialCount");
        var random = new Random(seed);
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
    public int ConsumeAtWorldPosition(double worldX, double worldY, double cellSize, int requestedAmount)
    {
        int x, y;
        if (requestedAmount <= 0 || !TryWorldToCell(worldX, worldY, cellSize, out x, out y)) return 0;
        int index = y * Width + x;
        int consumed = Math.Min(current[index], requestedAmount);
        current[index] -= consumed;
        return consumed;
    }

    public void Step(int maximum, int increase, int birthNeighborMinimum, bool growthEnabled, bool wrap)
    {
        if (maximum < 1 || increase < 0 || birthNeighborMinimum < 0 || birthNeighborMinimum > 8)
            throw new ArgumentOutOfRangeException("rule");
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int index = y * Width + x;
            int amount = current[index];
            if (amount == 0)
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
                next[index] = neighbors >= birthNeighborMinimum ? InitialAmount : 0;
            }
            else next[index] = (int)Math.Min(maximum, (long)amount + (growthEnabled ? increase : 0));
        }
        var previous = current;
        current = next;
        next = previous;
    }
}
