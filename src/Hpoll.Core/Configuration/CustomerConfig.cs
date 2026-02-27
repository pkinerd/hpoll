namespace Hpoll.Core.Configuration;

public class HpollSettings
{
    public List<CustomerConfig> Customers { get; set; } = new();
    public PollingSettings Polling { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
    public HueAppSettings HueApp { get; set; } = new();
}

public class CustomerConfig
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Australia/Sydney";
    public List<HubConfig> Hubs { get; set; } = new();
}

public class HubConfig
{
    public string BridgeId { get; set; } = string.Empty;
    public string HueApplicationKey { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; }
}

public class PollingSettings
{
    public int IntervalMinutes { get; set; } = 60;
}

public class EmailSettings
{
    public List<string> SendTimesUtc { get; set; } = new() { "08:00" };
    public string FromAddress { get; set; } = string.Empty;
    public string AwsRegion { get; set; } = "us-east-1";
}

public class HueAppSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
