namespace Hpoll.Core.Configuration;

/// <summary>
/// Settings for the hub polling background service. Bound from the "Polling" configuration section.
/// </summary>
public class PollingSettings
{
    /// <summary>Minutes between polling cycles for motion and temperature sensors.</summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Hours between battery-level polls (battery changes slowly, so this is much less frequent than sensor polling).</summary>
    public int BatteryPollIntervalHours { get; set; } = 84;

    /// <summary>Hours of sensor reading history to retain; older readings are pruned each cycle.</summary>
    public int DataRetentionHours { get; set; } = 168;

    /// <summary>HTTP request timeout in seconds for Hue API calls.</summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>How often (in hours) the token refresh service checks for expiring tokens.</summary>
    public int TokenRefreshCheckHours { get; set; } = 24;

    /// <summary>Tokens expiring within this many hours are proactively refreshed.</summary>
    public int TokenRefreshThresholdHours { get; set; } = 48;

    /// <summary>Maximum consecutive token refresh attempts before marking a hub as needing re-auth.</summary>
    public int TokenRefreshMaxRetries { get; set; } = 3;
}

/// <summary>
/// Settings for the email scheduler and renderer. Bound from the "Email" configuration section.
/// </summary>
public class EmailSettings
{
    /// <summary>Default daily send times in UTC (e.g. "08:00", "20:00"). Overridden per-customer by SendTimesLocal.</summary>
    public List<string> SendTimesUtc { get; set; } = new();

    /// <summary>Verified SES sender address (From header).</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>AWS region for the SES client.</summary>
    public string AwsRegion { get; set; } = "us-east-1";

    /// <summary>Battery % below which devices appear in the email alert section.</summary>
    public int BatteryAlertThreshold { get; set; } = 60;
    /// <summary>Battery % below which the email battery bar is rendered red.</summary>
    public int BatteryLevelCritical { get; set; } = 30;
    /// <summary>Battery % below which the email battery bar is rendered yellow (green above this level).</summary>
    public int BatteryLevelWarning { get; set; } = 50;

    /// <summary>Number of hours each activity summary window covers in the email report.</summary>
    public int SummaryWindowHours { get; set; } = 4;

    /// <summary>Number of activity summary windows to include in each email.</summary>
    public int SummaryWindowCount { get; set; } = 7;

    /// <summary>Minutes to wait before retrying after an email sending failure.</summary>
    public int ErrorRetryDelayMinutes { get; set; } = 5;
}

/// <summary>
/// Philips Hue Remote API OAuth application credentials. Bound from the "HueApp" configuration section.
/// </summary>
public class HueAppSettings
{
    /// <summary>OAuth Client ID from the Hue developer portal.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth Client Secret from the Hue developer portal.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>OAuth redirect URI registered with the Hue developer portal.</summary>
    public string CallbackUrl { get; set; } = string.Empty;
}

/// <summary>
/// Settings for the automatic database backup service. Bound from the "Backup" configuration section.
/// </summary>
public class BackupSettings
{
    /// <summary>Hours between automatic backup runs.</summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>Number of backup files to retain; oldest are pruned after each backup.</summary>
    public int RetentionCount { get; set; } = 7;

    /// <summary>Subdirectory under DataPath where backup files are stored.</summary>
    public string SubDirectory { get; set; } = "backups";
}

/// <summary>
/// Settings for the admin web portal. PasswordHash is read from the ADMIN_PASSWORD_HASH environment variable.
/// </summary>
public class AdminSettings
{
    /// <summary>BCrypt hash of the admin password. Null or empty triggers first-time setup mode.</summary>
    public string? PasswordHash { get; set; }
}
