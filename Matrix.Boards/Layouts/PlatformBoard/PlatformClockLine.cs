using System;
using Matrix.Core.Abstractions;
using Matrix.Core.Rendering;
using Matrix.Fonts;

namespace Matrix.Boards.PlatformBoard;

/// <summary>
/// Renders the clock line buffer for the PlatformBoard.
/// The buffer is 64×9 pixels. Draws "HH:mm:" in the Clock font followed
/// immediately by "ss" in the Page font, all left-aligned from x=0.
/// Each second digit occupies a fixed 9px-wide cell.
/// </summary>
public static class PlatformClockLine
{
    public const int Width  = 64;
    public const int Height = 9;

    public static void Render(ILineBuffer buffer, DateTime time)
    {
        IFont clockFont = FontRegistry.GetFont("Clock");
        IFont pageFont  = FontRegistry.GetFont("Page");

        string hhmm = time.ToString("HH:mm:");
        string ss   = time.ToString("ss");

        // Draw "HH:mm:" in Clock font starting at x=0
        TextRenderer.DrawString(buffer, clockFont, hhmm, startX: 0, startY: 0);

        // Immediately follow with "ss" in Page font, bottom-aligned.
        // Each character is drawn centred within a fixed 9px-wide cell.
        int ssStartX = TextRenderer.MeasureStringWidth(clockFont, hhmm) + clockFont.Spacing;
        int ssStartY = GetBottomAlignedY(pageFont, ss, buffer.Height);

        DrawFixedWidth(buffer, pageFont, ss, cellWidth: 9, startX: ssStartX, startY: ssStartY);
    }

    /// <summary>
    /// Draws each character of <paramref name="text"/> centred within a fixed
    /// <paramref name="cellWidth"/>-pixel column, ignoring the glyph's natural width.
    /// </summary>
    private static void DrawFixedWidth(
        ILineBuffer buffer, IFont font, string text, int cellWidth, int startX, int startY)
    {
        int cellX = startX;
        foreach (char c in text)
        {
            if (font.ContainsGlyph(c))
            {
                IGlyph glyph  = font.GetGlyph(c);
                int    offset = (cellWidth - glyph.Width) / 2; // centre within cell
                TextRenderer.DrawString(buffer, font, c.ToString(), startX: cellX + offset, startY: startY);
            }
            cellX += cellWidth;
        }
    }

    /// <summary>
    /// Returns a startY such that the lowest ink pixel in <paramref name="text"/>
    /// sits flush with the bottom of a buffer of <paramref name="bufferHeight"/> pixels.
    /// Falls back to 0 if no glyphs have any set pixels.
    /// </summary>
    private static int GetBottomAlignedY(IFont font, string text, int bufferHeight)
    {
        int lowestInkRow = 0;
        foreach (char c in text)
        {
            if (!font.ContainsGlyph(c)) continue;
            IGlyph g = font.GetGlyph(c);
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                    if (g.GetPixel(x, y) && y > lowestInkRow)
                        lowestInkRow = y;
        }
        // lowestInkRow is 0-based, so the ink spans (lowestInkRow + 1) rows
        return bufferHeight - (lowestInkRow + 1);
    }
}
