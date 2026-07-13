using System;
using System.Collections.Generic;
using Matrix.Boards.Models;

namespace Matrix.Boards.DepartureLineup;

public class DepartureLineupContext : StationContext
{
    public HardwareDiagnosticState HardwareState { get; set; } = new();
}
