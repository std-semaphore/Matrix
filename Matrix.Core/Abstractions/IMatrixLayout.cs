using System.Collections.Generic;

namespace Matrix.Core.Abstractions;

public interface IMatrixLayout<in TContext>
{
    int RowHeight { get; }
    int TotalRows { get; }

    int GetTotalPages(TContext context, int screenWidth);
    void RenderFrame(List<ILineBuffer> buffers, TContext context, int pageIndex, int tickCounter);
}