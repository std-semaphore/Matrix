namespace Matrix.Core.Abstractions;

public interface ILineBuffer
{
    int Width { get; }
    int Height { get; }

    void SetPixel(int x, int y, bool on);
    bool GetPixel(int x, int y);
    void Clear();
    void Fill();
}