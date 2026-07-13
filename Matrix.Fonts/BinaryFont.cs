using System.Reflection;

namespace Matrix.Fonts;

public class BinaryFont : IFont
{
    public string Name { get; }
    public int Height { get; }
    private readonly Dictionary<char, GlyphData> _glyphs = new();

    public BinaryFont(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName) 
                           ?? throw new FileNotFoundException($"Font {resourceName} not found.");
        using var reader = new BinaryReader(stream);

        Name = new string(reader.ReadChars(4)); 
        Height = reader.ReadInt16();
        int count = reader.ReadInt16();

        for (int i = 0; i < count; i++)
        {
            char c = reader.ReadChar();
            int width = reader.ReadByte();
            int dataLength = (int)Math.Ceiling((width * Height) / 8.0);
            byte[] pixels = reader.ReadBytes(dataLength);

            _glyphs[c] = new GlyphData(width, Height, pixels);
        }
    }

    public IGlyph GetGlyph(char c) => _glyphs.TryGetValue(c, out var g) ? g : throw new Exception($"Glyph {c} not found");
    public bool ContainsGlyph(char c) => _glyphs.ContainsKey(c);
}

internal class GlyphData : IGlyph
{
    public int Width { get; }
    public int Height { get; }
    private readonly byte[] _pixels;

    public GlyphData(int w, int h, byte[] p) { Width = w; Height = h; _pixels = p; }

    public bool GetPixel(int x, int y) 
    {
        int bitIndex = y * Width + x;
        return (_pixels[bitIndex / 8] & (1 << (7 - (bitIndex % 8)))) != 0;
    }

    public byte GetScanline(int y) => _pixels[y * (int)Math.Ceiling(Width / 8.0)];
}