namespace Matrix.Fonts;

public interface IGlyph {
    int Width { get; }
    int Height { get; }
    bool GetPixel(int x, int y);
    byte GetScanline(int y);
}

public interface IFont {
    string Name { get; }
    int Height { get; }
    IGlyph GetGlyph(char c);
}