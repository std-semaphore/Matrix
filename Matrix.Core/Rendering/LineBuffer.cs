using Matrix.Core.Abstractions;

namespace Matrix.Core.Rendering;

public class LineBuffer : ILineBuffer
{
    private readonly bool[,] _pixels;

    public int Width { get; }
    public int Height { get; }

    public LineBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new bool[width, height];
    }

    public void SetPixel(int x, int y, bool on)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            _pixels[x, y] = on;
        }
    }

    public bool GetPixel(int x, int y)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            return _pixels[x, y];
        }
        return false;
    }

    public void Clear()
    {
        Array.Clear(_pixels, 0, _pixels.Length);
    }

    public void Fill()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _pixels[x, y] = true;
            }
        }
    }
}