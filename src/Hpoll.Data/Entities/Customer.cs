namespace Hpoll.Data.Entities;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Australia/Sydney";
    public string CcEmails { get; set; } = string.Empty; // comma-separated CC addresses
    public string BccEmails { get; set; } = string.Empty; // comma-separated BCC addresses
    public string Status { get; set; } = "active"; // active, inactive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<Hub> Hubs { get; set; } = new();
}
