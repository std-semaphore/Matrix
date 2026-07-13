using System.Reflection;

namespace Matrix.Fonts;

public static class FontRegistry
{
    private static readonly Dictionary<string, BinaryFont> _loadedFonts = new();
    private static readonly List<string> _fontResourceNames = new();

    static FontRegistry()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
                                .Where(n => n.EndsWith(".mtxfont", StringComparison.OrdinalIgnoreCase));
        _fontResourceNames.AddRange(resources);
    }

    public static IEnumerable<string> GetAvailableFontNames() =>
        _fontResourceNames.Select(r => r.Split('.').Reverse().Skip(1).First());

    public static IFont GetFont(string name)
    {
        if (_loadedFonts.TryGetValue(name, out var cached)) return cached;

        string? fullPath = _fontResourceNames.FirstOrDefault(r => 
            r.EndsWith($".Resources.{name}.mtxfont", StringComparison.OrdinalIgnoreCase));

        if (fullPath == null) throw new FileNotFoundException($"Embedded font '{name}' not found");

        var font = new BinaryFont(fullPath);
        _loadedFonts[name] = font;
        return font;
    }
}