namespace Hpoll.Data.Entities;

public class Hub
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string HueBridgeId { get; set; } = string.Empty;
    public string HueApplicationKey { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; }
    public string Status { get; set; } = "active"; // active, inactive, needs_reauth
    public DateTime? LastPolledAt { get; set; }
    public DateTime? LastBatteryPollUtc { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<Device> Devices { get; set; } = new();
    public List<PollingLog> PollingLogs { get; set; } = new();
}
