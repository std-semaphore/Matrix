using System;
using System.Linq;
using Matrix.Core.Abstractions;
using Matrix.Core.Rendering;
using Matrix.Fonts;
using Matrix.Boards.Models;

namespace Matrix.Boards.DepartureLineup.Rendering;

public static class DepartureRowRenderer
{
    private const int DestColX = 32;
    private const int PlatColX = 185;
    private const int PlatColW = 20;
    private const int ExpColX = 195;
    private const int DestinationAlternateTicks = 50; 

    public static void Render(ILineBuffer[] allocatedLines, Departure train, int tickCounter)
    {
        ILineBuffer mainLine = allocatedLines[0];
        IFont standardFont = FontRegistry.GetFont("Standard");

        TextRenderer.DrawString(mainLine, standardFont, train.ScheduledTime.ToString("HH:mm"), startX: 0, startY: 0);

        string destLabel = GetDestinationDisplayText(train, tickCounter);
        TextRenderer.DrawString(mainLine, standardFont, destLabel, startX: DestColX, startY: 0);

        if (!train.IsCancelled && !string.IsNullOrEmpty(train.Platform))
        {
            string platStr = train.IsPlatformConfirmed ? train.Platform : $"Plat {train.Platform}*";
            TextRenderer.DrawAlignedString(mainLine, standardFont, platStr, TextAlignment.Left, VerticalAlignment.Top, boxStartX: PlatColX, boxWidth: PlatColW);
        }

        string statusLabel = "Delayed";
        if (!string.IsNullOrEmpty(train.LiveStatusOverride)) statusLabel = train.LiveStatusOverride;
        else if (train.IsCancelled) statusLabel = "Cancelled";
        else if (train.EstimatedTime.HasValue)
        {
            statusLabel = train.EstimatedTime.Value == train.ScheduledTime ? "On Time" : train.EstimatedTime.Value.ToString("HH:mm");
        }
        
        TextRenderer.DrawAlignedString(mainLine, standardFont, statusLabel, TextAlignment.Right, VerticalAlignment.Top, boxStartX: ExpColX, boxWidth: mainLine.Width - ExpColX);

        int subCursor = 1;

        if (!string.IsNullOrEmpty(train.Via) && subCursor < allocatedLines.Length)
        {
            TextRenderer.DrawString(allocatedLines[subCursor++], standardFont, $"via {train.Via}", startX: DestColX, startY: 0);
        }

        string? incidentMsg = GetReasonText(train);
        if (!string.IsNullOrEmpty(incidentMsg) && subCursor < allocatedLines.Length)
        {
            TextRenderer.DrawString(allocatedLines[subCursor++], standardFont, incidentMsg, startX: DestColX, startY: 0);
        }

        var splitPoint = train.CallingPoints.FirstOrDefault(cp => cp.DoesTrainDivideHere);
        if (splitPoint != null && subCursor < allocatedLines.Length)
        {
            string portionLabel = GetDividingTrainText(splitPoint, tickCounter);
            TextRenderer.DrawString(allocatedLines[subCursor++], standardFont, portionLabel, startX: DestColX, startY: 0);
        }
    }

    private static string GetDestinationDisplayText(Departure train, int tickCounter)
    {
        var destinations = train.Destinations;
        if (destinations == null || destinations.Count == 0) return string.Empty;
        if (destinations.Count == 1) return destinations[0];

        bool isDividing = train.CallingPoints.Any(cp => cp.DoesTrainDivideHere);
        if (!isDividing)
        {
            string combined = string.Join(" & ", destinations);
            int availableWidth = PlatColX - DestColX;
            IFont standardFont = FontRegistry.GetFont("Standard");
            if (TextRenderer.MeasureStringWidth(standardFont, combined) <= availableWidth)
            {
                return combined;
            }
        }

        int alternateIdx = (tickCounter / DestinationAlternateTicks) % destinations.Count;
        string altDest = destinations[alternateIdx];
        return alternateIdx < destinations.Count - 1 ? $"{altDest} &" : altDest;
    }

    private static string GetDividingTrainText(CallingPoint splitPoint, int tickCounter)
    {
        int portionToggle = (tickCounter / DestinationAlternateTicks) % 2;
        if (portionToggle == 0 && splitPoint.MainPortionCoachesAfterDivide.HasValue)
            return $"Front {splitPoint.MainPortionCoachesAfterDivide.Value} coaches";
        if (portionToggle == 1 && splitPoint.DetachedPortionCoachesAfterDivide.HasValue)
            return $"Rear {splitPoint.DetachedPortionCoachesAfterDivide.Value} coaches";
        
        return "Train divides";
    }

    private static string? GetReasonText(Departure d)
    {
        if (d.IsCancelled && !string.IsNullOrEmpty(d.CancellationReason)) return $"Due to {d.CancellationReason}";
        if (!string.IsNullOrEmpty(d.DelayReason)) return $"Due to {d.DelayReason}";
        if (d.SpecialNotices.Count > 0) return string.Join(" ", d.SpecialNotices);
        return null;
    }
}