using System.Text.Json.Serialization;

namespace Hpoll.Core.Models;

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

public class HueResourceRef
{
    [JsonPropertyName("rid")]
    public string Rid { get; set; } = string.Empty;

    [JsonPropertyName("rtype")]
    public string Rtype { get; set; } = string.Empty;
}

public class HueMotionResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

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

public class HueMotionReport
{
    [JsonPropertyName("motion")]
    public bool Motion { get; set; }

    [JsonPropertyName("changed")]
    public DateTime Changed { get; set; }
}

public class HueTemperatureResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

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

public class HueDevicePowerResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("owner")]
    public HueResourceRef Owner { get; set; } = new();

    [JsonPropertyName("power_state")]
    public HuePowerState PowerState { get; set; } = new();
}

public class HuePowerState
{
    [JsonPropertyName("battery_state")]
    public string? BatteryState { get; set; }

    [JsonPropertyName("battery_level")]
    public int? BatteryLevel { get; set; }
}

public class HueBridgeResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("bridge_id")]
    public string BridgeId { get; set; } = string.Empty;
}
