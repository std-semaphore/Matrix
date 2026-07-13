using System;
using System.Collections.Generic;
using Matrix.Core.Rendering;
using Matrix.Boards.Models;
using Matrix.Boards.DepartureLineup;

namespace Matrix.Boards.DepartureLineup;

public class DepartureLineupBoard
{
    private readonly MatrixBoardInstance<DepartureLineupContext> _board;
    private readonly DepartureLineupContext _context;

    public DepartureLineupContext Context => _context;

    public DepartureLineupBoard(string stationName = "Kestby Station")
    {
        _context = new DepartureLineupContext
        {
            StationName = stationName
        };
        _board = new MatrixBoardInstance<DepartureLineupContext>(new DepartureLineupLayout(), width: 250, ticksPerPage: 150);
    }

    public void UpdateTrainDepartures(List<Departure>? departures)
    {
        _context.MasterTimetable = departures ?? new List<Departure>();
    }

    public byte[] UpdateTime(DateTime currentTime)
    {
        _context.ServerTime = currentTime;
        return _board.Update(_context);
    }
}
