namespace Matrix.Boards.Models;

public class StationContext
{
    public string StationName { get; set; } = "Station";
    public DateTime ServerTime { get; set; }
    public List<Departure> MasterTimetable { get; set; } = new();
    public List<string> GlobalNotices { get; set; } = new();
}