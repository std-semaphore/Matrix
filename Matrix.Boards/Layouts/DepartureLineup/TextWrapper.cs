using System.Collections.Generic;
using System.Text;
using Matrix.Fonts;
using Matrix.Core.Rendering;

namespace Matrix.Boards.DepartureLineup.Formatting;

public static class TextWrapper
{
    public static List<string> WrapText(string text, IFont font, int maxWidth, double fillThreshold = 0.65)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;

        var words = text.Split(' ');
        var current = new StringBuilder();

        foreach (var word in words)
        {
            string candidate = current.Length == 0 ? word : current.ToString() + " " + word;
            int candidateWidth = TextRenderer.MeasureStringWidth(font, candidate);

            if (candidateWidth > maxWidth && current.Length > 0)
            {
                lines.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
            else
            {
                current.Clear();
                current.Append(candidate);
            }

            if (TextRenderer.MeasureStringWidth(font, current.ToString()) >= maxWidth * fillThreshold)
            {
                lines.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0) lines.Add(current.ToString());
        return lines;
    }
}
