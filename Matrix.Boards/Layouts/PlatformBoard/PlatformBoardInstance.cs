using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Matrix.Core.Abstractions;
using Matrix.Core.Rendering;
using Matrix.Boards.Models;

namespace Matrix.Boards.PlatformBoard;

/// <summary>
/// Manages all buffers for the PlatformBoard and drives the render loop.
/// The board has three 200×9 message lines and one 64×9 clock line.
/// </summary>
public class PlatformBoardInstance
{
    private readonly int _refreshCycleTicks;
    private readonly LineBuffer[] _msgBuffers;
    private readonly LineBuffer   _clockBuffer;
    private int _tickCounter;
    private int _animationTickCounter;
    private string? _lastApproachingTrainId;
    private DateTime _approachingExpiry;
    private string _prevDeparturesHash = "";
    private bool _forceRefresh;
    private PlatformBoardMode? _pendingMode;

    public PlatformBoardContext Context { get; }

    public PlatformBoardInstance(
        string diagBoardDescriptor = "P1124-CBV-MT1(Plat2)",
        string diagAddress         = "Address = 120 (32h)",
        string diagIpAddress       = "IP = 10.243.26.14",
        int refreshCycleTicks      = 7200) // Default 2 minutes refresh cycle at 60fps
    {
        _refreshCycleTicks = refreshCycleTicks;
        Context = new PlatformBoardContext
        {
            DiagBoardDescriptor = diagBoardDescriptor,
            DiagAddress         = diagAddress,
            DiagIpAddress       = diagIpAddress,
        };

        _msgBuffers = new LineBuffer[]
        {
            new(PlatformMessageLines.Width, PlatformMessageLines.Height),
            new(PlatformMessageLines.Width, PlatformMessageLines.Height),
            new(PlatformMessageLines.Width, PlatformMessageLines.Height),
        };

        _clockBuffer = new LineBuffer(PlatformClockLine.Width, PlatformClockLine.Height);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Changes the display mode cleanly with a fade refresh.
    /// </summary>
    public void SetMode(PlatformBoardMode mode)
    {
        if (Context.Mode == mode) return;
        _pendingMode = mode;
        _forceRefresh = true;
    }

    /// <summary>
    /// Displays a message across the three lines for the given duration,
    /// then automatically returns to Departures mode.
    /// </summary>
    public void ShowMessage(string line1, string line2, string line3, TimeSpan duration)
    {
        Context.MessageLine1  = line1;
        Context.MessageLine2  = line2;
        Context.MessageLine3  = line3;
        Context.MessageExpiry = Context.ServerTime + duration;
        SetMode(PlatformBoardMode.Message);
    }

    /// <summary>
    /// Updates the train departures list.
    /// </summary>
    public void UpdateTrainDepartures(List<Matrix.Boards.Models.Departure>? departures)
    {
        var list = departures ?? new List<Matrix.Boards.Models.Departure>();
        Context.Departures = list;

        string newHash = string.Join(",", list.Select(d => d.TrainId));
        if (newHash != _prevDeparturesHash)
        {
            _prevDeparturesHash = newHash;
            _forceRefresh = true;
        }
    }

    /// <summary>
    /// Advances the board by one tick and returns the rendered output.
    /// </summary>
    public PlatformBoardOutput Update(DateTime serverTime)
    {
        Context.ServerTime = serverTime;

        // Check for approaching pass train (only if no mode change is already pending)
        if (Context.Mode == PlatformBoardMode.Departures && _pendingMode == null)
        {
            foreach (var train in Context.Departures)
            {
                if (train.TrainId == _lastApproachingTrainId)
                    continue;

                foreach (var cp in train.CallingPoints)
                {
                    if (cp.Pass)
                    {
                        var passTime = cp.EstimatedTime ?? cp.ScheduledTime;
                        var diff = passTime - serverTime;
                        if (diff.TotalSeconds >= 0 && diff.TotalSeconds <= 30)
                        {
                            _pendingMode = PlatformBoardMode.Approaching;
                            _approachingExpiry = serverTime.AddSeconds(15);
                            _lastApproachingTrainId = train.TrainId;
                            _forceRefresh = true;
                            break;
                        }
                    }
                }
                if (_pendingMode == PlatformBoardMode.Approaching)
                    break;
            }
        }
        else if (Context.Mode == PlatformBoardMode.Approaching && _pendingMode == null)
        {
            if (serverTime >= _approachingExpiry)
            {
                _pendingMode = PlatformBoardMode.Departures;
                _forceRefresh = true;
            }
        }

        // Force a fade-out if departures have changed or we have a pending mode transition
        if (_forceRefresh)
        {
            _tickCounter = _refreshCycleTicks - 60;
            _forceRefresh = false;
        }

        _tickCounter++;

        // If a timed message has expired, fall back to Departures cleanly
        if (Context.Mode == PlatformBoardMode.Message
            && Context.MessageExpiry.HasValue
            && serverTime >= Context.MessageExpiry.Value)
        {
            _pendingMode = PlatformBoardMode.Departures;
            _forceRefresh = true;
            Context.MessageExpiry = null;
        }

        // Fading refresh logic based on configurable refreshCycleTicks (60fps: 60 ticks total = 1s fade)
        int cycleTick = _tickCounter % _refreshCycleTicks;
        bool isFading = cycleTick >= (_refreshCycleTicks - 60);
        byte onColor = 255;

        if (isFading)
        {
            int fadeTick = cycleTick - (_refreshCycleTicks - 60);
            
            // Switch mode exactly at the middle frame of the fade (tick 30) when screen is fully black (onColor = 0)
            if (fadeTick == 30 && _pendingMode != null)
            {
                Context.Mode = _pendingMode.Value;
                _pendingMode = null;
                _animationTickCounter = 0;
            }

            if (fadeTick < 30)
            {
                // Fade out (0.5 seconds = 30 ticks)
                onColor = (byte)(255 * (30 - fadeTick) / 30);
            }
            else
            {
                // Fade in (0.5 seconds = 30 ticks)
                onColor = (byte)(255 * (fadeTick - 29) / 30);

                // Reset the animation tick counter to 0 so it stays empty (Y=9) during fade-in
                _animationTickCounter = 0;
            }
        }
        else
        {
            _animationTickCounter++;
        }

        // Clear all buffers
        foreach (var buf in _msgBuffers) buf.Clear();
        _clockBuffer.Clear();

        // Render using the _animationTickCounter for animations/scrolls
        PlatformBoardLayout.RenderMessageLines(
            _msgBuffers[0], _msgBuffers[1], _msgBuffers[2],
            Context, _animationTickCounter);

        PlatformClockLine.Render(_clockBuffer, serverTime);

        // Export (Clock stays at full brightness 255; Message lines fade)
        return new PlatformBoardOutput
        {
            MessageSheet = TextureExporter.ExportSpriteSheet(_msgBuffers, onColor),
            ClockSheet   = TextureExporter.ExportSpriteSheet(
                               new ILineBuffer[] { _clockBuffer }, onColor: 255),
        };
    }
}

public struct PlatformBoardOutput
{
    public byte[] MessageSheet { get; set; }
    public byte[] ClockSheet { get; set; }
}
