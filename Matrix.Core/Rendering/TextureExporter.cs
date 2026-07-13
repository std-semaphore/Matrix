using Matrix.Core.Abstractions;

namespace Matrix.Core.Rendering;

public static class TextureExporter
{
    public static byte[] ExportSpriteSheet(IReadOnlyList<ILineBuffer> buffers, byte onColor = 255, byte offColor = 0)
    {
        if (buffers.Count == 0) return Array.Empty<byte>();

        int width = buffers[0].Width;
        int heightPerLine = buffers[0].Height;
        int totalHeight = heightPerLine * buffers.Count;

        byte[] textureData = new byte[width * totalHeight * 4];

        for (int lineIdx = 0; lineIdx < buffers.Count; lineIdx++)
        {
            var buffer = buffers[lineIdx];
            
            for (int y = 0; y < heightPerLine; y++)
            {
                int globalY = (lineIdx * heightPerLine) + y;
                
                for (int x = 0; x < width; x++)
                {
                    bool isOn = buffer.GetPixel(x, y);
                    byte intensity = isOn ? onColor : offColor;

                    int pixelIndex = (globalY * width + x) * 4;
                    textureData[pixelIndex]     = intensity;
                    textureData[pixelIndex + 1] = intensity;
                    textureData[pixelIndex + 2] = intensity; 
                    textureData[pixelIndex + 3] = 255;       
                }
            }
        }

        return textureData;
    }
}