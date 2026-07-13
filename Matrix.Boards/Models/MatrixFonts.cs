namespace Matrix.Boards.Models;
using Matrix.Fonts;

public static class MatrixFonts
{
    public static IFont Standard => FontRegistry.GetFont("Standard");
    public static IFont Page => FontRegistry.GetFont("Page");
    public static IFont Clock => FontRegistry.GetFont("Clock");
    public static IFont Wide => FontRegistry.GetFont("Wide");
}
