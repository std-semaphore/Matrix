using Matrix.Fonts;
using Matrix.Core.Abstractions;

namespace Matrix.Core.Rendering;

public enum TextAlignment
{
    Left,
    Center,
    Right
}

public enum VerticalAlignment
{
    Top,
    Middle,
    Bottom
}

public static class TextRenderer
{
    public static void DrawString(ILineBuffer buffer, IFont font, string? text, int startX, int startY)
    {
        if (string.IsNullOrEmpty(text)) return;

        int currentX = startX;

        foreach (char c in text)
        {
            if (!font.ContainsGlyph(c)) continue; 

            IGlyph glyph = font.GetGlyph(c);

            for (int y = 0; y < glyph.Height; y++)
            {
                for (int x = 0; x < glyph.Width; x++)
                {
                    if (glyph.GetPixel(x, y) && (currentX + x) < buffer.Width && (startY + y) < buffer.Height)
                    {
                        buffer.SetPixel(currentX + x, startY + y, true);
                    }
                }
            }
            currentX += glyph.Width + font.Spacing;
        }
    }

    public static int MeasureStringWidth(IFont font, string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        int totalWidth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (font.ContainsGlyph(text[i]))
            {
                totalWidth += font.GetGlyph(text[i]).Width;
            }
            
            if (i < text.Length - 1)
            {
                totalWidth += font.Spacing;
            }
        }

        return totalWidth;
    }

    public static void DrawAlignedString(
        ILineBuffer buffer, 
        IFont font, 
        string? text, 
        TextAlignment horizontalAlign, 
        VerticalAlignment verticalAlign,
        int boxStartX = 0, 
        int boxStartY = 0,
        int? boxWidth = null,
        int? boxHeight = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        int availableWidth = boxWidth ?? (buffer.Width - boxStartX);
        int textWidth = MeasureStringWidth(font, text);
        int drawX = boxStartX;

        switch (horizontalAlign)
        {
            case TextAlignment.Right:
                drawX = boxStartX + availableWidth - textWidth;
                break;
            case TextAlignment.Center:
                drawX = boxStartX + (availableWidth - textWidth) / 2;
                break;
            case TextAlignment.Left:
            default:
                drawX = boxStartX;
                break;
        }

        int availableHeight = boxHeight ?? (buffer.Height - boxStartY);
        int fontHeight = font.Height;
        int drawY = boxStartY;

        switch (verticalAlign)
        {
            case VerticalAlignment.Bottom:
                drawY = boxStartY + availableHeight - fontHeight;
                break;
            case VerticalAlignment.Middle:
                drawY = boxStartY + (availableHeight - fontHeight) / 2;
                break;
            case VerticalAlignment.Top:
            default:
                drawY = boxStartY;
                break;
        }

        DrawString(buffer, font, text, drawX, drawY);
    }
}