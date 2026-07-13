namespace Matrix.Boards.Models;

public enum StopActivityStatus
{
    Pending,    
    Arrived,     
    Departed,  
    Passed      
}

public class CallingPoint
{
    public string StationName { get; set; } = string.Empty;
    public string CrsCode { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;

    public DateTime ScheduledTime { get; set; } 
    public DateTime? EstimatedTime { get; set; } 
    
    public StopActivityStatus Activity { get; set; } = StopActivityStatus.Pending;
    public bool IsCancelled { get; set; }

    public bool Pass { get; set; }

    public bool DoesTrainDivideHere { get; set; }
    public List<string>? DetachedPortionDestinations { get; set; }
    public int? MainPortionCoachesAfterDivide { get; set; }
    public int? DetachedPortionCoachesAfterDivide { get; set; }
}