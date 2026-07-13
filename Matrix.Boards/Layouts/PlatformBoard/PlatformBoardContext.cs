using System.Collections.Generic;
using Matrix.Boards.Models;

namespace Matrix.Boards.PlatformBoard;

public enum PlatformBoardMode
{
    Diagnostic,
    Message,
    Departures,
    Approaching
}

/// <summary>
/// Holds all runtime state for the PlatformBoard.
/// </summary>
public class PlatformBoardContext
{
    public DateTime ServerTime { get; set; }
    public PlatformBoardMode Mode { get; set; } = PlatformBoardMode.Diagnostic;

    // ── Diagnostic ────────────────────────────────────────────────────────────
    /// <summary>Board/unit identifier shown on line 1, e.g. "P1124-CBV-MT1(Plat2)"</summary>
    public string DiagBoardDescriptor { get; set; } = string.Empty;
    /// <summary>Hardware address shown on line 2, e.g. "Address = 120 (32h)"</summary>
    public string DiagAddress         { get; set; } = string.Empty;
    /// <summary>IP address shown on line 3, e.g. "IP = 10.243.26.14"</summary>
    public string DiagIpAddress       { get; set; } = string.Empty;

    // ── Message ───────────────────────────────────────────────────────────────
    public string    MessageLine1  { get; set; } = string.Empty;
    public string    MessageLine2  { get; set; } = string.Empty;
    public string    MessageLine3  { get; set; } = string.Empty;
    /// <summary>
    /// When set, the board automatically falls back to Departures mode once
    /// ServerTime passes this value.
    /// </summary>
    public DateTime? MessageExpiry { get; set; }

    // ── Departures ───────────────────────────────────────────────────────────
    public List<Departure> Departures { get; set; } = new();
}
