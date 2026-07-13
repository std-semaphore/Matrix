using System;
using System.Collections.Generic;
using System.Linq;
using Matrix.Core.Abstractions;
using Matrix.Core.Rendering;
using Matrix.Fonts;
using Matrix.Boards.Models;
using Matrix.Boards.DepartureLineup.Formatting;
using Matrix.Boards.DepartureLineup.Rendering;

namespace Matrix.Boards.DepartureLineup;

public class DepartureLineupLayout : IMatrixLayout<DepartureLineupContext>
{
    public int RowHeight => 9;
    public int TotalRows => 10;

    private static readonly string[] DebugInfoLines =
    {
        "CIS-N09-16:P1043-NTI<Plat1",
        "Addr  = 221 (DDh)",
        "DATA  = 57600,8,1,None",
        "SYNC  = 19200,8,1,None",
        "IP    = DISABLED",
        "Scrpt =",
        "P1043:PR42P-NT01:V4.2-FGW"
    };

    public int GetTotalPages(DepartureLineupContext context, int screenWidth)
    {
        if (context.HardwareState.IsTurnedOff || context.HardwareState.ShowDebugInfo) return 1;
        if (context.MasterTimetable.Count == 0) return 1 + context.GlobalNotices.Count;
        
        int pagesCount = 1;
        int linesAvailable = TotalRows - 1; 
        int linesUsed = 0;

        foreach (var train in context.MasterTimetable)
        {
            int needed = GetServiceLineRequirement(train);
            if (linesUsed > 0 && (linesUsed + needed) > linesAvailable)
            {
                pagesCount++;
                linesUsed = 0;
                linesAvailable = TotalRows - 2; 
            }
            linesUsed += needed;
        }

        return pagesCount + context.GlobalNotices.Count;
    }

    public void RenderFrame(List<ILineBuffer> buffers, DepartureLineupContext context, int pageIndex, int tickCounter)
    {
        if (context.HardwareState.IsTurnedOff) return; 
        
        if (context.HardwareState.ShowDebugInfo)
        {
            RenderDiagnostics(buffers, tickCounter);
            RenderFooter(buffers[TotalRows - 1], false, 0, 1, context.ServerTime, showClock: false);
            return;
        }

        var departurePageStarts = new List<int> { 0 };
        int linesAvailable = TotalRows - 1; 
        int linesUsed = 0;
        
        for (int i = 0; i < context.MasterTimetable.Count; i++)
        {
            int needed = GetServiceLineRequirement(context.MasterTimetable[i]);
            if (linesUsed > 0 && (linesUsed + needed) > linesAvailable)
            {
                departurePageStarts.Add(i);
                linesUsed = 0;
                linesAvailable = TotalRows - 2; 
            }
            linesUsed += needed;
        }

        int departurePageCount = context.MasterTimetable.Count == 0 ? 1 : departurePageStarts.Count;
        int noticePageCount = context.GlobalNotices.Count;
        int totalPages = departurePageCount + noticePageCount;

        int activePageIdx = 0;
        int departureTicks = 200; 
        int noticeTicks = 100;    
        int totalCycleTicks = (departurePageCount * departureTicks) + (noticePageCount * noticeTicks);

        if (totalCycleTicks > 0)
        {
            int cycleTick = tickCounter % totalCycleTicks;
            if (cycleTick < departurePageCount * departureTicks)
            {
                activePageIdx = cycleTick / departureTicks;
            }
            else
            {
                activePageIdx = departurePageCount + (cycleTick - departurePageCount * departureTicks) / noticeTicks;
            }
        }

        if (activePageIdx < departurePageCount)
        {
            RenderDepartures(buffers, context, departurePageStarts, activePageIdx, tickCounter);
        }
        else
        {
            int noticeIndex = activePageIdx - departurePageCount;
            RenderSpecialNotice(buffers, context.GlobalNotices, noticeIndex);
        }

        RenderFooter(buffers[TotalRows - 1], true, activePageIdx, totalPages, context.ServerTime, showClock: true);
    }

    private void RenderDepartures(List<ILineBuffer> buffers, DepartureLineupContext context, List<int> pageStarts, int pageIdx, int tickCounter)
    {
        if (context.MasterTimetable.Count == 0)
        {
            IFont wideFont = FontRegistry.GetFont("Wide");
            TextRenderer.DrawAlignedString(buffers[3], wideFont, "Welcome to", TextAlignment.Center, VerticalAlignment.Top);
            TextRenderer.DrawAlignedString(buffers[4], wideFont, context.StationName, TextAlignment.Center, VerticalAlignment.Top);
            return;
        }

        int trainIndex = pageStarts[pageIdx];
        int rowCursor = 0;

        if (pageIdx > 0)
        {
            IFont wideFont = FontRegistry.GetFont("Wide");
            TextRenderer.DrawString(buffers[0], wideFont, "Continued.......", startX: 0, startY: 0);
            rowCursor = 1;
        }

        while (rowCursor < (TotalRows - 1) && trainIndex < context.MasterTimetable.Count)
        {
            var train = context.MasterTimetable[trainIndex];
            int linesNeeded = GetServiceLineRequirement(train);
            
            if (rowCursor + linesNeeded > (TotalRows - 1)) break;

            var allocatedLines = buffers.Skip(rowCursor).Take(linesNeeded).ToArray();
            
            DepartureRowRenderer.Render(allocatedLines, train, tickCounter);
            
            rowCursor += linesNeeded;
            trainIndex++;
        }
    }

    private void RenderSpecialNotice(List<ILineBuffer> buffers, List<string> notices, int noticeIdx)
    {
        if (notices == null || noticeIdx < 0 || noticeIdx >= notices.Count) return;

        IFont pageFont = FontRegistry.GetFont("Page");
        IFont standardFont = FontRegistry.GetFont("Standard");

        TextRenderer.DrawAlignedString(buffers[1], pageFont, "Special notices", TextAlignment.Center, VerticalAlignment.Top);

        var wrappedLines = TextWrapper.WrapText(notices[noticeIdx], standardFont, buffers[0].Width);
        int printableLines = Math.Min(wrappedLines.Count, TotalRows - 4);
        
        for (int i = 0; i < printableLines; i++)
        {
            TextRenderer.DrawAlignedString(buffers[2 + i], standardFont, wrappedLines[i], TextAlignment.Center, VerticalAlignment.Top);
        }
    }

    private void RenderDiagnostics(List<ILineBuffer> buffers, int tickCounter)
    {
        bool visible = (tickCounter % 300) < 200; 
        if (!visible) return;

        IFont standardFont = FontRegistry.GetFont("Standard");
        int rowStart = Math.Max(0, ((TotalRows - 1) - DebugInfoLines.Length) / 2);
        for (int i = 0; i < DebugInfoLines.Length && (rowStart + i) < (TotalRows - 1); i++)
        {
            TextRenderer.DrawAlignedString(buffers[rowStart + i], standardFont, DebugInfoLines[i], TextAlignment.Center, VerticalAlignment.Top);
        }
    }

    private void RenderFooter(ILineBuffer footerLine, bool showPageText, int pageIndex, int totalPages, DateTime serverTime, bool showClock)
    {
        if (showPageText)
        {
            IFont pageFont = FontRegistry.GetFont("Page");
            string pageText = totalPages > 1 ? $"Page {pageIndex + 1} of {totalPages}" : "Page 1 of 1";
            TextRenderer.DrawString(footerLine, pageFont, pageText, startX: 0, startY: 0);
        }

        if (showClock)
        {
            IFont clockFont = FontRegistry.GetFont("Clock");
            string timeStr = serverTime.ToString("HH:mm:ss");
            var mappedChars = timeStr.Select((c, index) =>
            {
                if (index < 6) return c;
                return c switch
                {
                    '1' => 'a',
                    '2' => 'b',
                    '3' => 'c',
                    '4' => 'd',
                    '5' => 'e',
                    '6' => 'f',
                    '7' => 'g',
                    '8' => 'h',
                    '9' => 'i',
                    '0' => 'j',
                    _ => c
                };
            });
            string clockText = new string(mappedChars.ToArray());
            TextRenderer.DrawAlignedString(footerLine, clockFont, clockText, TextAlignment.Right, VerticalAlignment.Middle);
        }
    }

    private int GetServiceLineRequirement(Departure d)
    {
        int rows = 1;
        if (!string.IsNullOrEmpty(d.Via)) rows++;
        if ((d.IsCancelled && !string.IsNullOrEmpty(d.CancellationReason)) || !string.IsNullOrEmpty(d.DelayReason) || d.SpecialNotices.Count > 0) rows++;
        if (d.CallingPoints.Any(cp => cp.DoesTrainDivideHere)) rows++;
        return rows;
    }
}