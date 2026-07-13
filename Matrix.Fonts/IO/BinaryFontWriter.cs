namespace Matrix.Fonts;

public class BinaryFontWriter
{
    public static void Save(string path, string name, int height, List<GlyphEditor> glyphs)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write(name.ToCharArray()); 
        writer.Write((short)height);
        writer.Write((short)glyphs.Count);

        foreach (var g in glyphs)
        {
            writer.Write((ushort)g.Character);
            writer.Write((byte)g.Width);
            
            byte[] paddedData = PackGlyphPixels(g.Grid, g.Width, height);
            writer.Write(paddedData); 
        }
    }

    private static byte[] PackGlyphPixels(bool[,] grid, int width, int height)
    {
        int bytesPerRow = (int)Math.Ceiling(width / 8.0);
        byte[] packed = new byte[bytesPerRow * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (grid[x, y])
                {
                    int byteIndex = (y * bytesPerRow) + (x / 8);
                    int bitOffset = 7 - (x % 8);
                    packed[byteIndex] |= (byte)(1 << bitOffset);
                }
            }
        }
        return packed;
    }
}

public record GlyphEditor(char Character, int Width, bool[,] Grid);