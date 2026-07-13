using Matrix.Core.Abstractions;
using Matrix.Core.Rendering;
using Matrix.Fonts;

namespace Matrix.Boards.PlatformBoard;

/// <summary>
/// Renders the three message line buffers for the PlatformBoard.
/// Each buffer is 200×9 pixels. Text is drawn centred using the Standard font.
/// </summary>
public static class PlatformMessageLines
{
    public const int Width  = 200;
    public const int Height = 9;

    public static void Render(ILineBuffer line1, ILineBuffer line2, ILineBuffer line3,
                              string text1, string text2, string text3)
    {
        IFont font = FontRegistry.GetFont("Standard");

        DrawCentred(line1, font, text1);
        DrawCentred(line2, font, text2);
        DrawCentred(line3, font, text3);
    }

    private static void DrawCentred(ILineBuffer buffer, IFont font, string text)
    {
        TextRenderer.DrawAlignedString(
            buffer, font, text,
            TextAlignment.Center, VerticalAlignment.Top);
    }
}
