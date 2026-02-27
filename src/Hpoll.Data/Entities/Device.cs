namespace Hpoll.Data.Entities;

public class Device
{
    public int Id { get; set; }
    public int HubId { get; set; }
    public Hub Hub { get; set; } = null!;
    public string HueDeviceId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<DeviceReading> Readings { get; set; } = new();
}
