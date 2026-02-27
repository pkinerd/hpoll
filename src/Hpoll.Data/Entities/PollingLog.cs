namespace Hpoll.Data.Entities;

public class PollingLog
{
    public long Id { get; set; }
    public int HubId { get; set; }
    public Hub Hub { get; set; } = null!;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int ApiCallsMade { get; set; }
}
