using System;
using System.Collections.Generic;
using Matrix.Core.Abstractions;

namespace Matrix.Core.Rendering;

public class MatrixBoardInstance<TContext>
{
    private readonly List<ILineBuffer> _buffers;
    private readonly IMatrixLayout<TContext> _layout;
    private readonly int _screenWidth;
    private readonly int _ticksPerPage;
    
    private int _tickCounter;

    public MatrixBoardInstance(IMatrixLayout<TContext> layout, int width = 250, int ticksPerPage = 150)
    {
        _layout = layout;
        _screenWidth = width;
        _ticksPerPage = ticksPerPage;
        
        _buffers = new List<ILineBuffer>();
        
        for (int i = 0; i < _layout.TotalRows; i++)
        {
            _buffers.Add(new LineBuffer(_screenWidth, _layout.RowHeight));
        }
    }

    public byte[] Update(TContext context)
    {
        _tickCounter++;

        foreach (var buffer in _buffers)
        {
            buffer.Clear();
        }

        int totalPages = _layout.GetTotalPages(context, _screenWidth);
        int currentPageIndex = totalPages > 0 ? (_tickCounter / _ticksPerPage) % totalPages : 0;

        _layout.RenderFrame(_buffers, context, currentPageIndex, _tickCounter);

        return TextureExporter.ExportSpriteSheet(_buffers);
    }
}