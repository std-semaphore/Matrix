namespace Matrix.Boards.Models;

public enum ServiceTrackingStatus
{
    Scheduled,  
    AtOrigin,  
    EnRoute,       
    Approaching, 
    AtPlatform,    
    Delayed,     
    Cancelled     
}

public class Departure
{
    public string TrainId { get; set; } = string.Empty;     
    public DateTime ScheduledTime { get; set; }  
    public List<string> Destinations { get; set; } = new();    
    public string? Via { get; set; }                        
    
    public DateTime? EstimatedTime { get; set; }
    public string? LiveStatusOverride { get; set; }
    public string? Platform { get; set; }                     
    public bool IsPlatformConfirmed { get; set; } = true;    
    public ServiceTrackingStatus LiveStatus { get; set; } = ServiceTrackingStatus.Scheduled;
    
    public string CurrentLocationDescription { get; set; } = string.Empty; 
    public int MinutesDelayed { get; set; }
    
    public int? CoachCount { get; set; }                    
    public string OperatorName { get; set; } = "Unknown operator";     
    public string CrowdingStatus { get; set; } = "";
    
    public bool IsCancelled { get; set; }
    public string? DelayReason { get; set; }                 
    public string? CancellationReason { get; set; }          

    public List<string> SpecialNotices { get; set; } = new();

    public List<CallingPoint> CallingPoints { get; set; } = new();
}