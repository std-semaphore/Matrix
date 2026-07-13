using System;
using System.Collections.Generic;
using System.Linq;
using Matrix.Core.Abstractions;
using Matrix.Core.Rendering;
using Matrix.Fonts;
using Matrix.Boards.Models;

namespace Matrix.Boards.PlatformBoard;

/// <summary>
/// Renders the three 200×9 message line buffers of the PlatformBoard
/// according to the active <see cref="PlatformBoardMode"/>.
/// The 64×9 clock line is handled separately by <see cref="PlatformClockLine"/>.
/// </summary>
public static class PlatformBoardLayout
{
    public static void RenderMessageLines(
        ILineBuffer line1, ILineBuffer line2, ILineBuffer line3,
        PlatformBoardContext context, int tickCounter)
    {
        switch (context.Mode)
        {
            case PlatformBoardMode.Diagnostic:
                RenderDiagnostic(line1, line2, line3, context, tickCounter);
                break;

            case PlatformBoardMode.Message:
                RenderMessage(line1, line2, line3, context);
                break;

            case PlatformBoardMode.Departures:
                RenderDepartures(line1, line2, line3, context, tickCounter);
                break;

            case PlatformBoardMode.Approaching:
                RenderApproaching(line1, line2, line3);
                break;
        }
    }

    // ── Approaching ───────────────────────────────────────────────────────────

    private static void RenderApproaching(ILineBuffer line1, ILineBuffer line2, ILineBuffer line3)
    {
        IFont standardFont = FontRegistry.GetFont("Standard");

        TextRenderer.DrawAlignedString(line1, standardFont, "** PLEASE STAND CLEAR **", TextAlignment.Center, VerticalAlignment.Top);
        TextRenderer.DrawAlignedString(line2, standardFont, "The next train is not scheduled", TextAlignment.Center, VerticalAlignment.Top);
        TextRenderer.DrawAlignedString(line3, standardFont, "to stop at this platform", TextAlignment.Center, VerticalAlignment.Top);
    }

    // ── Departures ───────────────────────────────────────────────────────────

    private static void RenderDepartures(
        ILineBuffer line1, ILineBuffer line2, ILineBuffer line3,
        PlatformBoardContext context, int tickCounter)
    {
        IFont wideFont = FontRegistry.GetFont("Wide");
        IFont standardFont = FontRegistry.GetFont("Standard");

        // ── Case: No departures ──
        if (context.Departures == null || context.Departures.Count == 0)
        {
            TextRenderer.DrawAlignedString(line1, wideFont, "   No departures   ", TextAlignment.Center, VerticalAlignment.Top);
            TextRenderer.DrawAlignedString(line2, standardFont, "Please check timetable", TextAlignment.Center, VerticalAlignment.Top);
            TextRenderer.DrawAlignedString(line3, standardFont, "for service updates", TextAlignment.Center, VerticalAlignment.Top);
            return;
        }

        var train = context.Departures[0];

        // --- LINE 1 ---
        // Slide up at the start of the cycle (0-11 ticks, 12 ticks total, snappy)
        int animY1 = 0;
        if (tickCounter < 12)
        {
            animY1 = 9 - (tickCounter * 3) / 4;
        }

        // 1. Time on the left in Wide font (HH:mm format)
        string timeText = train.ScheduledTime.ToString("HH:mm");
        TextRenderer.DrawString(line1, wideFont, timeText, startX: 0, startY: animY1);

        // 2. Status / Expected Time on the right
        string statusText = GetStatusText(train);
        TextRenderer.DrawAlignedString(line1, standardFont, statusText, TextAlignment.Right, VerticalAlignment.Top, boxStartY: animY1);

        // 3. Destination (with Wide/Standard fallback and via alternation)
        int destStartX = 31;
        int statusWidth = TextRenderer.MeasureStringWidth(standardFont, statusText);
        int availableWidth1 = line1.Width - destStartX - statusWidth - 4;

        // Build list of items to alternate (destinations and via)
        var itemsToAlternate = new List<string>();
        foreach (var dest in train.Destinations)
        {
            itemsToAlternate.Add(FormatDestinationName(dest));
        }
        if (!string.IsNullOrEmpty(train.Via))
        {
            itemsToAlternate.Add("via " + FormatDestinationName(train.Via));
        }

        // Alternate items every 300 ticks (5 seconds)
        int altIdx = (tickCounter / 300) % itemsToAlternate.Count;
        string activeDestText = itemsToAlternate[altIdx];

        // Font selection: fallback to Standard font on Line 1 if Wide font does not fit
        IFont destFont = wideFont;
        if (TextRenderer.MeasureStringWidth(wideFont, activeDestText) > availableWidth1)
        {
            destFont = standardFont;
        }
        TextRenderer.DrawString(line1, destFont, activeDestText, startX: destStartX, startY: animY1);

        // --- LINE 2 ---
        // Construct scrollable labels list
        var labels = new List<string>();
        var dividingCp = train.CallingPoints.FirstOrDefault(cp => cp.DoesTrainDivideHere);
        if (dividingCp != null)
        {
            int divideIdx = train.CallingPoints.IndexOf(dividingCp);
            var frontPoints = train.CallingPoints.Skip(divideIdx + 1).ToList();
            var rearDests = dividingCp.DetachedPortionDestinations ?? new List<string>();

            labels.Add($"A {train.OperatorName} service which has {train.CoachCount ?? 4} carriages.");
            labels.Add($"Front {dividingCp.MainPortionCoachesAfterDivide ?? 2} coaches calling at: {FormatCallingPoints(frontPoints)}");
            labels.Add($"Rear {dividingCp.DetachedPortionCoachesAfterDivide ?? 2} coaches calling at: {FormatDestinationsList(rearDests)}   ({train.OperatorName})");
        }
        else
        {
            labels.Add($"A {train.OperatorName} service which has {train.CoachCount ?? 3} carriages.");
            if (train.CallingPoints.Count > 0)
            {
                labels.Add($"Calling at: {FormatCallingPoints(train.CallingPoints)}   ({train.OperatorName})");
            }
            else
            {
                labels.Add($"({train.OperatorName})");
            }
        }

        // Pre-calculate scroll durations (60fps: 12 lockup, 120 hold = 2 seconds, scroll speed = 0.88px/tick = 22/25)
        var labelDurations = new List<int>();
        int totalDuration = 0;
        for (int i = 0; i < labels.Count; i++)
        {
            int width = TextRenderer.MeasureStringWidth(standardFont, labels[i]);
            int scrollDuration = width > 200 ? (width * 25 + 21) / 22 : 0;
            int duration = 12 + 120 + scrollDuration;
            labelDurations.Add(duration);
            totalDuration += duration;
        }

        int cycleTick = tickCounter % totalDuration;
        int activeIdx = 0;
        int subTick = cycleTick;
        for (int i = 0; i < labels.Count; i++)
        {
            if (subTick < labelDurations[i])
            {
                activeIdx = i;
                break;
            }
            subTick -= labelDurations[i];
        }

        int animY2 = 0;
        int scrollX = 0;
        if (subTick < 12)
        {
            animY2 = 9 - (subTick * 3) / 4;
        }
        else if (subTick < 12 + 120)
        {
            animY2 = 0;
            scrollX = 0;
        }
        else
        {
            animY2 = 0;
            scrollX = -(((subTick - 132) * 22) / 25);
        }
        TextRenderer.DrawString(line2, standardFont, labels[activeIdx], startX: scrollX, startY: animY2);

        // --- LINE 3 ---
        // Rotates between 2nd, 3rd, and 4th departures and station special notices.
        // Collect all unique special notices from all departures
        var specialNotices = context.Departures
            .SelectMany(d => d.SpecialNotices)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .ToList();

        // Construct Line 3 phases
        var line3Phases = new List<(string type, int duration, object data)>();
        for (int i = 1; i <= 3; i++)
        {
            if (context.Departures.Count > i)
            {
                line3Phases.Add(("Departure", 600, context.Departures[i]));
            }
        }
        if (specialNotices.Count > 0)
        {
            line3Phases.Add(("Notice", 1800, specialNotices));
        }

        if (line3Phases.Count > 0)
        {
            int totalLine3Duration = line3Phases.Sum(p => p.duration);
            int line3Tick = tickCounter % totalLine3Duration;

            int currentPhaseIdx = 0;
            int subTick3 = line3Tick;
            for (int i = 0; i < line3Phases.Count; i++)
            {
                if (subTick3 < line3Phases[i].duration)
                {
                    currentPhaseIdx = i;
                    break;
                }
                subTick3 -= line3Phases[i].duration;
            }

            var activePhase = line3Phases[currentPhaseIdx];

            if (activePhase.type == "Departure")
            {
                var train3 = (Departure)activePhase.data;
                int animY3 = subTick3 < 12 ? 9 - (subTick3 * 3) / 4 : 0;

                // Draw rank
                string rankText = GetOrdinal(context.Departures.IndexOf(train3) + 1);
                int rankWidth = TextRenderer.MeasureStringWidth(standardFont, rankText);
                TextRenderer.DrawString(line3, standardFont, rankText, startX: 0, startY: animY3);

                // Draw time
                int innerGap = 5;
                string timeText3 = train3.ScheduledTime.ToString("HH:mm");
                int timeStartX3 = rankWidth + innerGap;
                int timeWidth3 = TextRenderer.MeasureStringWidth(standardFont, timeText3);
                TextRenderer.DrawString(line3, standardFont, timeText3, startX: timeStartX3, startY: animY3);

                // Draw status (right aligned)
                string statusText3 = GetStatusText(train3);
                TextRenderer.DrawAlignedString(line3, standardFont, statusText3, TextAlignment.Right, VerticalAlignment.Top, boxStartY: animY3);

                // Draw destination
                int destStartX3 = timeStartX3 + timeWidth3 + innerGap;
                int statusWidth3 = TextRenderer.MeasureStringWidth(standardFont, statusText3);
                int availableWidth3 = line3.Width - destStartX3 - statusWidth3 - 4;

                // Alternate multiple destinations and via on Line 3 if present
                var items3 = new List<string>();
                foreach (var dest in train3.Destinations)
                {
                    items3.Add(FormatDestinationName(dest));
                }
                if (!string.IsNullOrEmpty(train3.Via))
                {
                    items3.Add("via " + FormatDestinationName(train3.Via));
                }

                int altIdx3 = (tickCounter / 300) % items3.Count;
                string destText3 = items3[altIdx3];

                // Standard font destination on Line 3
                TextRenderer.DrawString(line3, standardFont, destText3, startX: destStartX3, startY: animY3);
            }
            else if (activePhase.type == "Notice")
            {
                string noticeText = string.Join(" | ", (List<string>)activePhase.data);
                int noticeWidth = TextRenderer.MeasureStringWidth(wideFont, noticeText);

                if (noticeWidth <= 200)
                {
                    // Fits on screen without scrolling
                    int animY3 = subTick3 < 12 ? 9 - (subTick3 * 3) / 4 : 0;
                    TextRenderer.DrawAlignedString(line3, wideFont, noticeText, TextAlignment.Center, VerticalAlignment.Top, boxStartY: animY3);
                }
                else
                {
                    // Scrolls in Wide font, loops continuously with a 2-second hold
                    int animY3 = 0;
                    int scrollX3 = 0;
                    if (subTick3 < 12)
                    {
                        animY3 = 9 - (subTick3 * 3) / 4;
                        scrollX3 = 0;
                    }
                    else if (subTick3 < 12 + 120)
                    {
                        animY3 = 0;
                        scrollX3 = 0;
                    }
                    else
                    {
                        animY3 = 0;
                        int elapsed = subTick3 - 132;
                        int totalOffset = (elapsed * 22) / 25;
                        int loopRange = noticeWidth + 100; // 100px gap between repetitions
                        scrollX3 = - (totalOffset % loopRange);
                    }
                    TextRenderer.DrawString(line3, wideFont, noticeText, startX: scrollX3, startY: animY3);
                }
            }
        }
    }

    // ── Helper Methods ────────────────────────────────────────────────────────

    private static string GetStatusText(Departure train)
    {
        if (train.IsCancelled) return "Cancelled";
        if (!string.IsNullOrEmpty(train.LiveStatusOverride)) return train.LiveStatusOverride;
        if (train.LiveStatus == ServiceTrackingStatus.AtPlatform) return "Arrived";
        // Approaching is ignored so it falls through to estimated/on-time!
        if (train.EstimatedTime.HasValue && train.EstimatedTime.Value != train.ScheduledTime)
        {
            return $"Exp {train.EstimatedTime.Value:HH:mm}";
        }
        return "On time";
    }

    private static string FormatDestinationName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (name.Contains("AAS") || name.Contains("Agincourt") || name.Contains("Anserham"))
        {
            return "Anserham Ag. Sq";
        }
        return name;
    }

    private static string FormatDestinationsList(List<string> dests)
    {
        if (dests == null || dests.Count == 0) return string.Empty;
        var formatted = dests.Select(FormatDestinationName).ToList();
        string res = "";
        if (formatted.Count == 1) res = formatted[0];
        else if (formatted.Count == 2) res = $"{formatted[0]} and {formatted[1]}";
        else
        {
            string initial = string.Join(", ", formatted.Take(formatted.Count - 1));
            res = $"{initial} and {formatted.Last()}";
        }
        return res + ".";
    }

    private static string FormatCallingPoints(List<CallingPoint> callingPoints)
    {
        if (callingPoints == null || callingPoints.Count == 0) return string.Empty;
        string res = "";
        if (callingPoints.Count == 1) res = $"{FormatDestinationName(callingPoints[0].StationName)} only";
        else
        {
            var names = callingPoints.Select(cp => FormatDestinationName(cp.StationName)).ToList();
            if (names.Count == 2) res = $"{names[0]} and {names[1]}";
            else
            {
                string initial = string.Join(", ", names.Take(names.Count - 1));
                res = $"{initial} and {names.Last()}";
            }
        }
        return res + ".";
    }

    private static string GetOrdinal(int num)
    {
        if (num <= 0) return string.Empty;
        switch (num % 100)
        {
            case 11:
            case 12:
            case 13:
                return num + "th";
        }
        switch (num % 10)
        {
            case 1: return num + "st";
            case 2: return num + "nd";
            case 3: return num + "rd";
            default: return num + "th";
        }
    }

    // ── Diagnostic ────────────────────────────────────────────────────────────

    private static void RenderDiagnostic(
        ILineBuffer line1, ILineBuffer line2, ILineBuffer line3,
        PlatformBoardContext context, int tickCounter)
    {
        // Blink: visible for 1200 ticks out of every 1800 (same cadence as DepartureLineupLayout, scaled to 60fps)
        bool visible = (tickCounter % 1800) < 1200;
        if (!visible) return;

        IFont font = FontRegistry.GetFont("Standard");

        TextRenderer.DrawString(line1, font, context.DiagBoardDescriptor, startX: 0, startY: 0);
        TextRenderer.DrawString(line2, font, context.DiagAddress,         startX: 0, startY: 0);
        TextRenderer.DrawString(line3, font, context.DiagIpAddress,       startX: 0, startY: 0);
    }

    // ── Message ───────────────────────────────────────────────────────────────

    private static void RenderMessage(
        ILineBuffer line1, ILineBuffer line2, ILineBuffer line3,
        PlatformBoardContext context)
    {
        IFont font = FontRegistry.GetFont("Standard");

        DrawCentred(line1, font, context.MessageLine1);
        DrawCentred(line2, font, context.MessageLine2);
        DrawCentred(line3, font, context.MessageLine3);
    }

    private static void DrawCentred(ILineBuffer buffer, IFont font, string text)
    {
        TextRenderer.DrawAlignedString(
            buffer, font, text,
            TextAlignment.Center, VerticalAlignment.Top);
    }
}
