using System.Text.Json.Serialization;

namespace Hpoll.Core.Models;

/// <summary>
/// Standard Hue CLIP v2 API response envelope containing errors and typed resource data.
/// </summary>
public class HueResponse<T>
{
    [JsonPropertyName("errors")]
    public List<HueError> Errors { get; set; } = new();

    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = new();
}

public class HueError
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Reference to another Hue resource. <see cref="Rid"/> is the resource ID and
/// <see cref="Rtype"/> is the resource type (e.g. "device", "motion", "temperature").
/// Used by <c>owner</c> to point to the parent device and by <c>services</c> to
/// list child service resources.
/// </summary>
public class HueResourceRef
{
    [JsonPropertyName("rid")]
    public string Rid { get; set; } = string.Empty;

    [JsonPropertyName("rtype")]
    public string Rtype { get; set; } = string.Empty;
}

/// <summary>
/// Motion sensor resource. The <c>owner</c> reference points to the parent device resource.
/// </summary>
public class HueMotionResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Reference to the parent device resource that owns this sensor.</summary>
    [JsonPropertyName("owner")]
    public HueResourceRef Owner { get; set; } = new();

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("motion")]
    public HueMotionData Motion { get; set; } = new();
}

public class HueMotionData
{
    [JsonPropertyName("motion_report")]
    public HueMotionReport? MotionReport { get; set; }
}

/// <summary>
/// Motion sensor report. <see cref="Motion"/> indicates whether motion is currently
/// detected. <see cref="Changed"/> is the last time the <c>motion</c> property value
/// changed (transitions both to <c>true</c> and back to <c>false</c>). The sensor
/// may return to <c>false</c> after a device-specific cooldown period.
/// </summary>
public class HueMotionReport
{
    [JsonPropertyName("motion")]
    public bool Motion { get; set; }

    [JsonPropertyName("changed")]
    public DateTime Changed { get; set; }
}

/// <summary>
/// Temperature sensor resource. The <c>owner</c> reference points to the parent device resource.
/// </summary>
public class HueTemperatureResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Reference to the parent device resource that owns this sensor.</summary>
    [JsonPropertyName("owner")]
    public HueResourceRef Owner { get; set; } = new();

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("temperature")]
    public HueTemperatureData Temperature { get; set; } = new();
}

public class HueTemperatureData
{
    [JsonPropertyName("temperature_report")]
    public HueTemperatureReport? TemperatureReport { get; set; }
}

/// <summary>
/// Temperature sensor report. <see cref="Temperature"/> is in degrees Celsius.
/// <see cref="Changed"/> is the last time the <c>temperature</c> value changed.
/// </summary>
public class HueTemperatureReport
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("changed")]
    public DateTime Changed { get; set; }
}

public class HueDeviceResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public HueDeviceMetadata Metadata { get; set; } = new();

    [JsonPropertyName("product_data")]
    public HueProductData ProductData { get; set; } = new();

    /// <summary>Child service resources (motion, temperature, device_power, etc.) belonging to this device.</summary>
    [JsonPropertyName("services")]
    public List<HueResourceRef> Services { get; set; } = new();
}

public class HueDeviceMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("archetype")]
    public string Archetype { get; set; } = string.Empty;
}

public class HueProductData
{
    [JsonPropertyName("model_id")]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("software_version")]
    public string SoftwareVersion { get; set; } = string.Empty;
}

/// <summary>
/// Device power resource reporting battery state and level.
/// The <c>owner</c> reference points to the parent device resource.
/// </summary>
public class HueDevicePowerResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Reference to the parent device resource that owns this power service.</summary>
    [JsonPropertyName("owner")]
    public HueResourceRef Owner { get; set; } = new();

    [JsonPropertyName("power_state")]
    public HuePowerState PowerState { get; set; } = new();
}

public class HuePowerState
{
    /// <summary>Battery health indicator: "normal", "low", or "critical".</summary>
    [JsonPropertyName("battery_state")]
    public string? BatteryState { get; set; }

    /// <summary>Battery level as a percentage (0–100).</summary>
    [JsonPropertyName("battery_level")]
    public int? BatteryLevel { get; set; }
}

public class HueBridgeResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The bridge's unique hardware identifier (MAC-derived).</summary>
    [JsonPropertyName("bridge_id")]
    public string BridgeId { get; set; } = string.Empty;
}
