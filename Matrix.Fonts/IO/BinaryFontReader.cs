namespace Matrix.Fonts;

public static class BinaryFontReader
{
    public static (string Name, int Height, List<GlyphEntry> Glyphs) Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        string name = new string(reader.ReadChars(4));
        int height = reader.ReadInt16();
        int count = reader.ReadInt16();

        var glyphs = new List<GlyphEntry>();

        for (int i = 0; i < count; i++)
        {
            char character = (char)reader.ReadUInt16();
            int width = reader.ReadByte();
            
            int bytesPerRow = (int)Math.Ceiling(width / 8.0);
            int dataLength = bytesPerRow * height;
            
            byte[] pixelData = reader.ReadBytes(dataLength);

            glyphs.Add(new GlyphEntry(character, width, pixelData));
        }

        return (name, height, glyphs);
    }
}
public record GlyphEntry(char Character, int Width, byte[] PixelData);