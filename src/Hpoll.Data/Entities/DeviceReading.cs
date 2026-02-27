namespace Hpoll.Data.Entities;

public class DeviceReading
{
    public long Id { get; set; }
    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ReadingType { get; set; } = string.Empty; // "motion" or "temperature"
    public string Value { get; set; } = string.Empty; // JSON: {"motion": true, "changed": "..."} or {"temperature": 21.5, "changed": "..."}
}
