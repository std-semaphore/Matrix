using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Matrix.Fonts;

namespace Matrix.FontEditor;

public class GlyphVM
{
    public char Character { get; set; }
    public int Width { get; set; }
    public bool[,] Grid { get; set; } = null!;

    public override string ToString() => $"'{Character}'";
}

public partial class MainWindow : Window
{
    private readonly ObservableCollection<GlyphVM> _glyphs = new();
    private int _fontHeight = 8;
    private int _spacing = 1;
    private bool _suppressEvents;

    private bool _isPainting;
    private bool _paintValue;

    private GlyphVM? _selectedGlyph;

    // PERFORMANCE OPTIMIZATION CACHES
    private readonly List<Ellipse> _canvasLedPool = new();
    private int _currentPoolCols;
    private int _currentPoolRows;
    private readonly Dictionary<char, Border[,]> _sheetTileCellRefs = new();

    private class GlyphSnapshot
    {
        public char Character;
        public int Width;
        public bool[,] Grid = null!;
    }

    private class FontSnapshot
    {
        public string Name = "";
        public int Height;
        public int Spacing;
        public char? SelectedChar;
        public List<GlyphSnapshot> Glyphs = new();
    }

    private class OldGlyphDto { public char Character { get; set; } public int Width { get; set; } public string Pixels { get; set; } = ""; }
    private class PixelCoordinate { public int X { get; set; } public int Y { get; set; } }
    private class OldFontDto { public string FontName { get; set; } = "Font"; public int Height { get; set; } public int Spacing { get; set; } public List<OldGlyphDto> Glyphs { get; set; } = new(); }

    private readonly List<FontSnapshot> _undoStack = new();
    private readonly List<FontSnapshot> _redoStack = new();
    private const int MaxUndoDepth = 200;

    private string? _currentFilePath;

    private static IBrush ActiveDot => SolidColorBrush.Parse("#fab387");
    private static IBrush InactiveDot => SolidColorBrush.Parse("#26263a");
    private static IBrush ActiveTileSelected => SolidColorBrush.Parse("#cba6f7");

    public MainWindow()
    {
        InitializeComponent();

        _suppressEvents = true;
        HeightBox.Text = _fontHeight.ToString();
        SpacingBox.Text = _spacing.ToString();
        _suppressEvents = false;

        NewButton.Click += (_, _) => NewFont();
        OpenButton.Click += async (_, _) => await OpenFont();
        SaveButton.Click += async (_, _) => await SaveFont(forceDialog: false);
        SaveAsButton.Click += async (_, _) => await SaveFont(forceDialog: true);
        
        var nativeMenu = NativeMenu.GetMenu(this);
        if (nativeMenu != null)
        {
            var fileMenu = nativeMenu.Items[0] as NativeMenuItem;
            if (fileMenu?.Menu != null)
            {
                if (fileMenu.Menu.Items[0] is NativeMenuItem item0) item0.Click += (_, _) => NewFont();
                if (fileMenu.Menu.Items[1] is NativeMenuItem item1) item1.Click += async (_, _) => await OpenFont();
                if (fileMenu.Menu.Items[3] is NativeMenuItem item3) item3.Click += async (_, _) => await SaveFont(forceDialog: false);
                if (fileMenu.Menu.Items[4] is NativeMenuItem item4) item4.Click += async (_, _) => await SaveFont(forceDialog: true);
            }

            var toolsMenu = nativeMenu.Items[1] as NativeMenuItem;
            if (toolsMenu?.Menu != null)
            {
                if (toolsMenu.Menu.Items[0] is NativeMenuItem tool0) tool0.Click += async (_, _) => await ConvertOldJsonFontFile();
            }
        }

        AddButton.Click += (_, _) => AddGlyph();
        RemoveButton.Click += (_, _) => RemoveGlyph();

        UndoButton.Click += (_, _) => PerformUndo();
        RedoButton.Click += (_, _) => PerformRedo();

        InvertButton.Click += (_, _) => InvertSelected();
        ClearButton.Click += (_, _) => ClearSelected();
        ShiftUpButton.Click += (_, _) => ShiftSelected(0, -1);
        ShiftDownButton.Click += (_, _) => ShiftSelected(0, 1);
        ShiftLeftButton.Click += (_, _) => ShiftSelected(-1, 0);
        ShiftRightButton.Click += (_, _) => ShiftSelected(1, 0);

        HeightBox.LostFocus += (_, _) => CommitHeightChange();
        HeightBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitHeightChange(); };

        SpacingBox.LostFocus += (_, _) => CommitSpacingChange();
        SpacingBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitSpacingChange(); };

        SelectedWidthBox.LostFocus += (_, _) => CommitSelectedWidthChange();
        SelectedWidthBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitSelectedWidthChange(); };

        PreviewBox.TextChanged += (_, _) => UpdatePlaygroundPreviewOnly();
        SearchBox.TextChanged += (_, _) => RebuildGlyphsTileGrid();

        PixelPanel.PointerPressed += PixelPanel_PointerPressed;
        PixelPanel.PointerMoved += PixelPanel_PointerMoved;

        this.PointerReleased += (_, _) => _isPainting = false;

        NewFont();
    }

    private async Task ConvertOldJsonFontFile()
    {
        var openFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select legacy .json font file",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("PlatformDisplay font file (*.json)") { Patterns = new[] { "*.json" } } }
        });

        if (openFiles.Count == 0) return;

        try
        {
            string jsonContent = await File.ReadAllTextAsync(openFiles[0].Path.LocalPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var oldFont = JsonSerializer.Deserialize<OldFontDto>(jsonContent, options);

            if (oldFont == null) throw new InvalidDataException("JSON parsing returned a null root structure reference.");

            var targetFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export conversion result",
                SuggestedFileName = $"{oldFont.FontName}.mtxfont",
                FileTypeChoices = new[] { new FilePickerFileType("Matrix font (*.mtxfont)") { Patterns = new[] { "*.mtxfont" } } }
            });

            if (targetFile == null) return;

            string binaryFontName = (oldFont.FontName ?? "FONT").PadRight(4).Substring(0, 4);
            var editorsList = new List<GlyphEditor>();
            
            _glyphs.Clear();
            _fontHeight = oldFont.Height;
            _spacing = oldFont.Spacing;

            _suppressEvents = true;
            HeightBox.Text = _fontHeight.ToString();
            SpacingBox.Text = _spacing.ToString();
            _suppressEvents = false;

            foreach (var oldGlyph in oldFont.Glyphs)
            {
                byte[] rawBytes;
                try
                {
                    string hex = oldGlyph.Pixels ?? "";
                    if (hex.Length % 2 != 0) hex += "0"; 
                    rawBytes = Convert.FromHexString(hex);
                }
                catch
                {
                    rawBytes = Array.Empty<byte>();
                }

                bool[,] grid = new bool[oldGlyph.Width, oldFont.Height];
                
                for (int y = 0; y < oldFont.Height; y++)
                {
                    for (int x = 0; x < oldGlyph.Width; x++)
                    {
                        int linearBitIndex = y * oldGlyph.Width + x;
                        int byteIndex = linearBitIndex / 8;
                        int bitOffset = linearBitIndex % 8;

                        if (byteIndex < rawBytes.Length)
                        {
                            if ((rawBytes[byteIndex] & (1 << bitOffset)) != 0)
                            {
                                grid[x, y] = true;
                            }
                        }
                    }
                }
                
                editorsList.Add(new GlyphEditor(oldGlyph.Character, oldGlyph.Width, grid));

                var memoryGrid = new bool[32, 32];
                for (int x = 0; x < oldGlyph.Width; x++)
                    for (int y = 0; y < oldFont.Height; y++)
                        memoryGrid[x, y] = grid[x, y];

                _glyphs.Add(new GlyphVM { Character = oldGlyph.Character, Width = oldGlyph.Width, Grid = memoryGrid });
            }

            BinaryFontWriter.Save(targetFile.Path.LocalPath, binaryFontName, oldFont.Height, editorsList);

            _currentFilePath = targetFile.Path.LocalPath;
            _selectedGlyph = _glyphs.Count > 0 ? _glyphs[0] : null;

            if (_selectedGlyph != null)
            {
                _suppressEvents = true;
                SelectedWidthBox.Text = _selectedGlyph.Width.ToString();
                _suppressEvents = false;
            }

            RebuildGlyphsTileGrid();
            BuildPixelGrid();
            FullRenderPipeline();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Data conversion failed: {ex.Message}");
        }
    }

    private void ClearToEmptyFont()
    {
        _glyphs.Clear();
        NameBox.Text = "";
        _fontHeight = 8;
        _spacing = 1;
        _suppressEvents = true;
        HeightBox.Text = _fontHeight.ToString();
        SpacingBox.Text = _spacing.ToString();
        _suppressEvents = false;
        _undoStack.Clear();
        _redoStack.Clear();
        _currentFilePath = null;
        _selectedGlyph = null;

        UpdateUndoRedoButtons();
        UpdateStatus();
        RebuildGlyphsTileGrid();
        BuildPixelGrid();
        FullRenderPipeline();
    }

    private void NewFont()
    {
        _glyphs.Clear();
        NameBox.Text = "";
        _fontHeight = 8;
        _spacing = 1;
        _suppressEvents = true;
        HeightBox.Text = _fontHeight.ToString();
        SpacingBox.Text = _spacing.ToString();
        _suppressEvents = false;
        _undoStack.Clear();
        _redoStack.Clear();
        _currentFilePath = null;

        for (char c = 'A'; c <= 'Z'; c++)
        {
            _glyphs.Add(new GlyphVM { Character = c, Width = 8, Grid = new bool[32, 32] });
        }

        _selectedGlyph = _glyphs[0];

        _suppressEvents = true;
        SelectedWidthBox.Text = _selectedGlyph.Width.ToString();
        _suppressEvents = false;

        UpdateUndoRedoButtons();
        UpdateStatus();
        RebuildGlyphsTileGrid();
        BuildPixelGrid();
        FullRenderPipeline();
    }

    private async Task<bool> OpenFont()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open matrix font",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Matrix font (*.mtxfont)") { Patterns = new[] { "*.mtxfont", "*.*" } } }
        });

        if (files.Count == 0) return false;
        var path = files[0].Path.LocalPath;

        var (name, height, glyphEntries) = BinaryFontReader.Load(path);

        _glyphs.Clear();
        NameBox.Text = name.TrimEnd('\0');
        _fontHeight = height;
        
        _suppressEvents = true;
        HeightBox.Text = height.ToString();
        _suppressEvents = false;

        foreach (var entry in glyphEntries)
        {
            var grid = Unpack(entry.PixelData, entry.Width, height);
            _glyphs.Add(new GlyphVM { Character = entry.Character, Width = entry.Width, Grid = grid });
        }

        _undoStack.Clear();
        _redoStack.Clear();
        UpdateUndoRedoButtons();

        _currentFilePath = path;
        UpdateStatus();

        _selectedGlyph = _glyphs.Count > 0 ? _glyphs[0] : null;
        
        RebuildGlyphsTileGrid();
        BuildPixelGrid();
        FullRenderPipeline();
        return true;
    }

    private async Task SaveFont(bool forceDialog)
    {
        string path;

        if (!forceDialog && _currentFilePath is not null)
        {
            path = _currentFilePath;
        }
        else
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save matrix font",
                SuggestedFileName = string.IsNullOrWhiteSpace(NameBox.Text) ? "font.mtxfont" : NameBox.Text + ".mtxfont",
                FileTypeChoices = new[] { new FilePickerFileType("Matrix font (*.mtxfont)") { Patterns = new[] { "*.mtxfont" } } }
            });

            if (file is null) return;
            path = file.Path.LocalPath;
        }

        string name = (NameBox.Text ?? "").PadRight(4).Substring(0, 4);
        var editors = _glyphs.Select(g => new GlyphEditor(g.Character, g.Width, SafeTruncateGrid(g.Grid, g.Width, _fontHeight))).ToList();

        BinaryFontWriter.Save(path, name, _fontHeight, editors);

        _currentFilePath = path;
        UpdateStatus();
    }

    private static bool[,] SafeTruncateGrid(bool[,] source, int w, int h)
    {
        var target = new bool[w, h];
        for (int x = 0; x < Math.Min(w, source.GetLength(0)); x++)
            for (int y = 0; y < Math.Min(h, source.GetLength(1)); y++)
                target[x, y] = source[x, y];
        return target;
    }

    private void RebuildGlyphsTileGrid()
    {
        GlyphsTileGrid.Children.Clear();
        string filter = SearchBox.Text?.Trim() ?? "";

        var filteredList = _glyphs.OrderBy(g => g.Character).ToList();
        if (!string.IsNullOrEmpty(filter))
        {
            filteredList = filteredList.Where(g => g.Character.ToString().Equals(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        foreach (var glyph in filteredList)
        {
            bool isSelected = (_selectedGlyph == glyph);

            var tileText = new TextBlock
            {
                Text = glyph.Character == ' ' ? "␣" : glyph.Character.ToString(),
                FontSize = 13,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = isSelected ? SolidColorBrush.Parse("#11111b") : SolidColorBrush.Parse("#cdd6f4")
            };

            var tileFrame = new Border
            {
                Width = 30,
                Height = 30,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Background = isSelected ? ActiveTileSelected : SolidColorBrush.Parse("#313244"),
                BorderBrush = isSelected ? ActiveTileSelected : SolidColorBrush.Parse("#45475a"),
                BorderThickness = new Thickness(1),
                Child = tileText,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            tileFrame.PointerPressed += (s, e) =>
            {
                _selectedGlyph = glyph;
                
                _suppressEvents = true;
                SelectedWidthBox.Text = glyph.Width.ToString();
                _suppressEvents = false;

                RebuildGlyphsTileGrid();
                BuildPixelGrid();
            };

            GlyphsTileGrid.Children.Add(tileFrame);
        }
    }

    private void AddGlyph()
    {
        if (string.IsNullOrEmpty(CharBox.Text)) return;
        char c = CharBox.Text[0];

        if (!int.TryParse(WidthBox.Text, out int width) || width <= 0) return;

        PushUndo();

        var glyph = new GlyphVM
        {
            Character = c,
            Width = width,
            Grid = new bool[32, 32]
        };

        var existing = _glyphs.FirstOrDefault(g => g.Character == c);
        if (existing != null) _glyphs.Remove(existing);

        _glyphs.Add(glyph);
        _selectedGlyph = glyph;

        _suppressEvents = true;
        SelectedWidthBox.Text = width.ToString();
        _suppressEvents = false;

        RebuildGlyphsTileGrid();
        BuildPixelGrid();
        FullRenderPipeline();
    }

    private void RemoveGlyph()
    {
        if (_selectedGlyph == null) return;

        PushUndo();
        _glyphs.Remove(_selectedGlyph);

        _selectedGlyph = _glyphs.Count > 0 ? _glyphs[0] : null;

        _suppressEvents = true;
        SelectedWidthBox.Text = _selectedGlyph != null ? _selectedGlyph.Width.ToString() : "";
        _suppressEvents = false;

        RebuildGlyphsTileGrid();
        BuildPixelGrid();
        FullRenderPipeline();
    }

    private void CommitSelectedWidthChange()
    {
        if (_suppressEvents) return;
        if (_selectedGlyph == null) return;
        if (!int.TryParse(SelectedWidthBox.Text, out int newWidth) || newWidth <= 0) return;
        if (newWidth == _selectedGlyph.Width) return;

        PushUndo();
        _selectedGlyph.Width = newWidth;

        BuildPixelGrid();
        FullRenderPipeline();
    }

    private void CommitHeightChange()
    {
        if (_suppressEvents) return;
        if (!int.TryParse(HeightBox.Text, out int newHeight) || newHeight <= 0) return;
        if (newHeight == _fontHeight) return;

        PushUndo();
        _fontHeight = newHeight;

        BuildPixelGrid();
        FullRenderPipeline();
    }

    private void CommitSpacingChange()
    {
        if (_suppressEvents) return;
        if (!int.TryParse(SpacingBox.Text, out int newSpacing) || newSpacing < 0) return;
        _spacing = newSpacing;
        UpdatePlaygroundPreviewOnly();
    }

    private void InvertSelected()
    {
        if (_selectedGlyph == null) return;
        PushUndo();
        for (int x = 0; x < _selectedGlyph.Width; x++)
            for (int y = 0; y < _fontHeight; y++)
                _selectedGlyph.Grid[x, y] = !_selectedGlyph.Grid[x, y];
        BuildPixelGrid();
        OnActiveGlyphMutated();
    }

    private void ClearSelected()
    {
        if (_selectedGlyph == null) return;
        PushUndo();
        for (int x = 0; x < 32; x++)
            for (int y = 0; y < 32; y++)
                _selectedGlyph.Grid[x, y] = false;
        BuildPixelGrid();
        OnActiveGlyphMutated();
    }

    private void ShiftSelected(int dx, int dy)
    {
        if (_selectedGlyph == null) return;
        PushUndo();

        var newGrid = new bool[32, 32];

        for (int x = 0; x < 32; x++)
        {
            for (int y = 0; y < 32; y++)
            {
                int srcX = x - dx;
                int srcY = y - dy;

                if (srcX >= 0 && srcX < 32 && srcY >= 0 && srcY < 32)
                {
                    newGrid[x, y] = _selectedGlyph.Grid[srcX, srcY];
                }
            }
        }

        _selectedGlyph.Grid = newGrid;
        BuildPixelGrid();
        OnActiveGlyphMutated();
    }

    private Border[,]? _cellRefs;

    private void BuildPixelGrid()
    {
        PixelPanel.Children.Clear();
        _cellRefs = null;

        if (_selectedGlyph == null)
        {
            PixelPanel.Columns = 1;
            return;
        }

        PixelPanel.Columns = _selectedGlyph.Width;
        _cellRefs = new Border[_selectedGlyph.Width, _fontHeight];

        const double cellSize = 24;

        for (int y = 0; y < _fontHeight; y++)
        {
            for (int x = 0; x < _selectedGlyph.Width; x++)
            {
                var cell = new Border
                {
                    Width = cellSize,
                    Height = cellSize,
                    Margin = new Thickness(1.5),
                    CornerRadius = new CornerRadius(cellSize / 2),
                    Background = _selectedGlyph.Grid[x, y] ? ActiveDot : InactiveDot
                };
                _cellRefs[x, y] = cell;
                PixelPanel.Children.Add(cell);
            }
        }
    }

    private void PixelPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_selectedGlyph == null) return;

        var point = e.GetCurrentPoint(PixelPanel);
        bool isRightButton = point.Properties.IsRightButtonPressed;

        _isPainting = true;
        _paintValue = !isRightButton;

        PushUndo();
        PaintAt(e.GetPosition(PixelPanel));
        e.Handled = true;
    }

    private void PixelPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPainting) return;
        PaintAt(e.GetPosition(PixelPanel));
    }

    private void PaintAt(Point pos)
    {
        if (_selectedGlyph == null || _cellRefs is null) return;
        if (PixelPanel.Bounds.Width <= 0 || PixelPanel.Bounds.Height <= 0) return;

        double cellW = PixelPanel.Bounds.Width / _selectedGlyph.Width;
        double cellH = PixelPanel.Bounds.Height / _fontHeight;

        int cx = (int)(pos.X / cellW);
        int cy = (int)(pos.Y / cellH);

        if (cx < 0 || cx >= _selectedGlyph.Width || cy < 0 || cy >= _fontHeight) return;

        if (_selectedGlyph.Grid[cx, cy] != _paintValue)
        {
            _selectedGlyph.Grid[cx, cy] = _paintValue;
            _cellRefs[cx, cy].Background = _paintValue ? ActiveDot : InactiveDot;

            UpdatePlaygroundPreviewOnly();
            UpdateSingleCheatSheetTile(_selectedGlyph, cx, cy);
        }
    }

    private void FullRenderPipeline()
    {
        UpdatePlaygroundPreviewOnly();
        RebuildEntireCheatSheetMonitor();
    }

    private void OnActiveGlyphMutated()
    {
        UpdatePlaygroundPreviewOnly();
        if (_selectedGlyph != null && _sheetTileCellRefs.TryGetValue(_selectedGlyph.Character, out var cellRefs))
        {
            for (int x = 0; x < _selectedGlyph.Width; x++)
                for (int y = 0; y < _fontHeight; y++)
                    cellRefs[x, y].Background = _selectedGlyph.Grid[x, y] ? ActiveDot : InactiveDot;
        }
    }

    private void UpdateSingleCheatSheetTile(GlyphVM glyph, int cx, int cy)
    {
        if (_sheetTileCellRefs.TryGetValue(glyph.Character, out var cellRefs))
        {
            if (cx < cellRefs.GetLength(0) && cy < cellRefs.GetLength(1))
            {
                cellRefs[cx, cy].Background = glyph.Grid[cx, cy] ? ActiveDot : InactiveDot;
            }
        }
    }

    private void UpdatePlaygroundPreviewOnly()
    {
        string text = PreviewBox.Text ?? "";

        const double ledSize = 4.5;
        const double ledStep = 6.0;
        int totalCanvasColumns = (int)(UnifiedPreviewCanvas.Width / ledStep);

        // 1. Manage and resize our cached shapes pool instead of clearing children entirely
        int targetTotalLeds = totalCanvasColumns * _fontHeight;
        if (_canvasLedPool.Count != targetTotalLeds || _currentPoolCols != totalCanvasColumns || _currentPoolRows != _fontHeight)
        {
            UnifiedPreviewCanvas.Children.Clear();
            _canvasLedPool.Clear();
            _currentPoolCols = totalCanvasColumns;
            _currentPoolRows = _fontHeight;

            for (int y = 0; y < _fontHeight; y++)
            {
                for (int x = 0; x < totalCanvasColumns; x++)
                {
                    var led = new Ellipse { Width = ledSize, Height = ledSize, Fill = InactiveDot };
                    Canvas.SetLeft(led, x * ledStep);
                    Canvas.SetTop(led, y * ledStep);
                    UnifiedPreviewCanvas.Children.Add(led);
                    _canvasLedPool.Add(led);
                }
            }
        }

        for (int i = 0; i < _canvasLedPool.Count; i++)
        {
            _canvasLedPool[i].Fill = InactiveDot;
        }

        int matrixCursorColumn = 2;
        foreach (char c in text)
        {
            var glyph = _glyphs.FirstOrDefault(g => g.Character == c);
            if (glyph == null) 
            {
                matrixCursorColumn += 4 + _spacing; 
                continue;
            }

            for (int y = 0; y < _fontHeight; y++)
            {
                for (int x = 0; x < glyph.Width; x++)
                {
                    int targetedCol = matrixCursorColumn + x;
                    if (targetedCol >= 0 && targetedCol < totalCanvasColumns)
                    {
                        if (glyph.Grid[x, y])
                        {
                            int index = y * totalCanvasColumns + targetedCol;
                            _canvasLedPool[index].Fill = ActiveDot;
                        }
                    }
                }
            }
            matrixCursorColumn += glyph.Width + _spacing;
        }
    }

    private void RebuildEntireCheatSheetMonitor()
    {
        FullFontSheetPanel.Children.Clear();
        _sheetTileCellRefs.Clear();
        
        var sortedGlyphsList = _glyphs.OrderBy(g => g.Character).ToList();
        const double sheetLedSize = 3.0;

        foreach (var glyph in sortedGlyphsList)
        {
            var cellSheetBlock = new StackPanel { Spacing = 3, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            var miniGrid = new Avalonia.Controls.Primitives.UniformGrid { Columns = glyph.Width, Rows = _fontHeight, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };

            var tileCellCache = new Border[glyph.Width, _fontHeight];

            for (int y = 0; y < _fontHeight; y++)
            {
                for (int x = 0; x < glyph.Width; x++)
                {
                    var dotBorder = new Border
                    {
                        Width = sheetLedSize,
                        Height = sheetLedSize,
                        Margin = new Thickness(0.3),
                        CornerRadius = new CornerRadius(sheetLedSize / 2),
                        Background = glyph.Grid[x, y] ? ActiveDot : InactiveDot
                    };
                    tileCellCache[x, y] = dotBorder;
                    miniGrid.Children.Add(dotBorder);
                }
            }

            _sheetTileCellRefs[glyph.Character] = tileCellCache;

            var labelText = new TextBlock
            {
                Text = glyph.Character == ' ' ? "Space" : glyph.Character.ToString(),
                FontSize = 10,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Foreground = SolidColorBrush.Parse("#a6adc8")
            };

            cellSheetBlock.Children.Add(miniGrid);
            cellSheetBlock.Children.Add(labelText);

            var outerFrameBorder = new Border
            {
                Background = SolidColorBrush.Parse("#181825"),
                BorderBrush = SolidColorBrush.Parse("#313244"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4),
                CornerRadius = new CornerRadius(4),
                Child = cellSheetBlock
            };

            FullFontSheetPanel.Children.Add(outerFrameBorder);
        }
    }


    private FontSnapshot CaptureSnapshot()
    {
        var snap = new FontSnapshot
        {
            Name = NameBox.Text ?? "",
            Height = _fontHeight,
            Spacing = _spacing,
            SelectedChar = _selectedGlyph?.Character
        };

        foreach (var g in _glyphs)
        {
            snap.Glyphs.Add(new GlyphSnapshot
            {
                Character = g.Character,
                Width = g.Width,
                Grid = (bool[,])g.Grid.Clone()
            });
        }

        return snap;
    }

    private void RestoreSnapshot(FontSnapshot snap)
    {
        _suppressEvents = true;

        NameBox.Text = snap.Name;
        _fontHeight = snap.Height;
        _spacing = snap.Spacing;
        HeightBox.Text = _fontHeight.ToString();
        SpacingBox.Text = _spacing.ToString();

        _glyphs.Clear();
        foreach (var gs in snap.Glyphs)
        {
            _glyphs.Add(new GlyphVM { Character = gs.Character, Width = gs.Width, Grid = gs.Grid });
        }

        if (snap.SelectedChar.HasValue)
        {
            _selectedGlyph = _glyphs.FirstOrDefault(g => g.Character == snap.SelectedChar.Value);
        }
        else
        {
            _selectedGlyph = null;
        }

        _suppressEvents = false;

        RebuildGlyphsTileGrid();
        BuildPixelGrid();
        FullRenderPipeline();
    }

    private void PushUndo()
    {
        _undoStack.Add(CaptureSnapshot());
        if (_undoStack.Count > MaxUndoDepth) _undoStack.RemoveAt(0);
        _redoStack.Clear();
        UpdateUndoRedoButtons();
    }

    private void PerformUndo()
    {
        if (_undoStack.Count == 0) return;

        var snap = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        _redoStack.Add(CaptureSnapshot());
        if (_redoStack.Count > MaxUndoDepth) _redoStack.RemoveAt(0);

        RestoreSnapshot(snap);
        UpdateUndoRedoButtons();
    }

    private void PerformRedo()
    {
        if (_redoStack.Count == 0) return;

        var snap = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);

        _undoStack.Add(CaptureSnapshot());
        if (_undoStack.Count > MaxUndoDepth) _undoStack.RemoveAt(0);

        RestoreSnapshot(snap);
        UpdateUndoRedoButtons();
    }

    private void UpdateUndoRedoButtons()
    {
        UndoButton.IsEnabled = _undoStack.Count > 0;
        RedoButton.IsEnabled = _redoStack.Count > 0;
    }

    private void UpdateStatus()
    {
        if (_currentFilePath is not null)
        {
            this.Title = $"Font Editor ({System.IO.Path.GetFileName(_currentFilePath)})";
        }
        else
        {
            this.Title = "Font Editor (New)";
        }
    }

    private static bool[,] Unpack(byte[] pixels, int width, int height)
    {
        var grid = new bool[32, 32];
        int bytesPerRow = (int)Math.Ceiling(width / 8.0);

        for (int y = 0; y < Math.Min(height, 32); y++)
        {
            for (int x = 0; x < Math.Min(width, 32); x++)
            {
                int byteIndex = y * bytesPerRow + x / 8;
                int bitOffset = 7 - (x % 8);
                grid[x, y] = (pixels[byteIndex] & (1 << bitOffset)) != 0;
            }
        }

        return grid;
    }
}