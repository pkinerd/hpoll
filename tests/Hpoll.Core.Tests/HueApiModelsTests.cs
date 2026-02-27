using System.Text.Json;
using Hpoll.Core.Models;
using Xunit;

namespace Hpoll.Core.Tests;

public class HueApiModelsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public void DeserializeMotionResponse_WithMotionReport_Succeeds()
    {
        var json = """
        {
            "errors": [],
            "data": [
                {
                    "id": "abc-123",
                    "type": "motion",
                    "owner": {
                        "rid": "dev-456",
                        "rtype": "device"
                    },
                    "enabled": true,
                    "motion": {
                        "motion_report": {
                            "motion": true,
                            "changed": "2026-02-27T10:00:00Z"
                        }
                    }
                }
            ]
        }
        """;

        var result = JsonSerializer.Deserialize<HueResponse<HueMotionResource>>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Empty(result.Errors);
        Assert.Single(result.Data);

        var motion = result.Data[0];
        Assert.Equal("abc-123", motion.Id);
        Assert.Equal("motion", motion.Type);
        Assert.Equal("dev-456", motion.Owner.Rid);
        Assert.Equal("device", motion.Owner.Rtype);
        Assert.True(motion.Enabled);
        Assert.NotNull(motion.Motion.MotionReport);
        Assert.True(motion.Motion.MotionReport.Motion);
        Assert.Equal(new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), motion.Motion.MotionReport.Changed);
    }

    [Fact]
    public void DeserializeMotionResponse_WithoutMotionReport_HandlesNull()
    {
        var json = """
        {
            "errors": [],
            "data": [
                {
                    "id": "abc-789",
                    "type": "motion",
                    "owner": {
                        "rid": "dev-012",
                        "rtype": "device"
                    },
                    "enabled": true,
                    "motion": {}
                }
            ]
        }
        """;

        var result = JsonSerializer.Deserialize<HueResponse<HueMotionResource>>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Single(result.Data);
        Assert.Null(result.Data[0].Motion.MotionReport);
    }

    [Fact]
    public void DeserializeTemperatureResponse_Succeeds()
    {
        var json = """
        {
            "errors": [],
            "data": [
                {
                    "id": "temp-001",
                    "type": "temperature",
                    "owner": {
                        "rid": "dev-456",
                        "rtype": "device"
                    },
                    "enabled": true,
                    "temperature": {
                        "temperature_report": {
                            "temperature": 21.5,
                            "changed": "2026-02-27T12:30:00Z"
                        }
                    }
                }
            ]
        }
        """;

        var result = JsonSerializer.Deserialize<HueResponse<HueTemperatureResource>>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Empty(result.Errors);
        Assert.Single(result.Data);

        var temp = result.Data[0];
        Assert.Equal("temp-001", temp.Id);
        Assert.Equal("temperature", temp.Type);
        Assert.Equal("dev-456", temp.Owner.Rid);
        Assert.Equal("device", temp.Owner.Rtype);
        Assert.True(temp.Enabled);
        Assert.NotNull(temp.Temperature.TemperatureReport);
        Assert.Equal(21.5, temp.Temperature.TemperatureReport.Temperature);
        Assert.Equal(new DateTime(2026, 2, 27, 12, 30, 0, DateTimeKind.Utc), temp.Temperature.TemperatureReport.Changed);
    }

    [Fact]
    public void DeserializeDeviceResponse_WithServices_Succeeds()
    {
        var json = """
        {
            "errors": [],
            "data": [
                {
                    "id": "device-001",
                    "type": "device",
                    "metadata": {
                        "name": "Living Room Sensor",
                        "archetype": "unknown_archetype"
                    },
                    "product_data": {
                        "model_id": "SML001",
                        "product_name": "Hue motion sensor",
                        "software_version": "1.1.27575"
                    },
                    "services": [
                        {
                            "rid": "motion-001",
                            "rtype": "motion"
                        },
                        {
                            "rid": "temp-001",
                            "rtype": "temperature"
                        },
                        {
                            "rid": "light-001",
                            "rtype": "light_level"
                        }
                    ]
                }
            ]
        }
        """;

        var result = JsonSerializer.Deserialize<HueResponse<HueDeviceResource>>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Empty(result.Errors);
        Assert.Single(result.Data);

        var device = result.Data[0];
        Assert.Equal("device-001", device.Id);
        Assert.Equal("device", device.Type);
        Assert.Equal("Living Room Sensor", device.Metadata.Name);
        Assert.Equal("unknown_archetype", device.Metadata.Archetype);
        Assert.Equal("SML001", device.ProductData.ModelId);
        Assert.Equal("Hue motion sensor", device.ProductData.ProductName);
        Assert.Equal("1.1.27575", device.ProductData.SoftwareVersion);
        Assert.Equal(3, device.Services.Count);
        Assert.Equal("motion-001", device.Services[0].Rid);
        Assert.Equal("motion", device.Services[0].Rtype);
    }

    [Fact]
    public void DeserializeTokenResponse_Succeeds()
    {
        var json = """
        {
            "access_token": "new-access-token-xyz",
            "refresh_token": "new-refresh-token-abc",
            "token_type": "bearer",
            "expires_in": 604800
        }
        """;

        var result = JsonSerializer.Deserialize<HueTokenResponse>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal("new-access-token-xyz", result.AccessToken);
        Assert.Equal("new-refresh-token-abc", result.RefreshToken);
        Assert.Equal("bearer", result.TokenType);
        Assert.Equal(604800, result.ExpiresIn);
    }

    [Fact]
    public void DeserializeErrorResponse_Succeeds()
    {
        var json = """
        {
            "errors": [
                {
                    "description": "unauthorized: invalid access token"
                },
                {
                    "description": "resource not found"
                }
            ],
            "data": []
        }
        """;

        var result = JsonSerializer.Deserialize<HueResponse<HueMotionResource>>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal("unauthorized: invalid access token", result.Errors[0].Description);
        Assert.Equal("resource not found", result.Errors[1].Description);
        Assert.Empty(result.Data);
    }
}
