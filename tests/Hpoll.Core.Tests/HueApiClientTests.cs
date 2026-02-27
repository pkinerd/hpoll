using System.Net;
using System.Text;
using System.Text.Json;
using Hpoll.Core.Configuration;
using Hpoll.Core.Models;
using Hpoll.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hpoll.Core.Tests;

public class HueApiClientTests
{
    private const string TestAccessToken = "test-access-token";
    private const string TestApplicationKey = "test-app-key";
    private const string TestClientId = "test-client-id";
    private const string TestClientSecret = "test-client-secret";
    private const string TestRefreshToken = "test-refresh-token";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly MockHttpMessageHandler _mockHandler;
    private readonly HueApiClient _client;

    public HueApiClientTests()
    {
        _mockHandler = new MockHttpMessageHandler();

        var httpClient = new HttpClient(_mockHandler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var settings = Options.Create(new HpollSettings
        {
            HueApp = new HueAppSettings
            {
                ClientId = TestClientId,
                ClientSecret = TestClientSecret
            }
        });

        var logger = new Mock<ILogger<HueApiClient>>();

        _client = new HueApiClient(mockFactory.Object, settings, logger.Object);
    }

    [Fact]
    public async Task GetMotionSensorsAsync_SendsCorrectHeaders()
    {
        var responseBody = new HueResponse<HueMotionResource> { Data = new List<HueMotionResource>() };
        _mockHandler.ConfigureResponse(HttpStatusCode.OK, JsonSerializer.Serialize(responseBody, JsonOptions));

        await _client.GetMotionSensorsAsync(TestAccessToken, TestApplicationKey);

        Assert.NotNull(_mockHandler.CapturedRequest);
        Assert.Equal("Bearer", _mockHandler.CapturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal(TestAccessToken, _mockHandler.CapturedRequest.Headers.Authorization?.Parameter);
        Assert.Contains(_mockHandler.CapturedRequest.Headers.GetValues("hue-application-key"), v => v == TestApplicationKey);
        Assert.Contains("/resource/motion", _mockHandler.CapturedRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetMotionSensorsAsync_ReturnsDeserializedData()
    {
        var json = """
        {
            "errors": [],
            "data": [
                {
                    "id": "motion-001",
                    "type": "motion",
                    "owner": { "rid": "dev-001", "rtype": "device" },
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
        _mockHandler.ConfigureResponse(HttpStatusCode.OK, json);

        var result = await _client.GetMotionSensorsAsync(TestAccessToken, TestApplicationKey);

        Assert.NotNull(result);
        Assert.Single(result.Data);
        Assert.Equal("motion-001", result.Data[0].Id);
        Assert.True(result.Data[0].Motion.MotionReport!.Motion);
    }

    [Fact]
    public async Task GetDevicesAsync_ReturnsDeviceList()
    {
        var json = """
        {
            "errors": [],
            "data": [
                {
                    "id": "device-001",
                    "type": "device",
                    "metadata": { "name": "Kitchen Sensor", "archetype": "unknown_archetype" },
                    "product_data": { "model_id": "SML001", "product_name": "Hue motion sensor", "software_version": "1.0" },
                    "services": [
                        { "rid": "motion-001", "rtype": "motion" }
                    ]
                },
                {
                    "id": "device-002",
                    "type": "device",
                    "metadata": { "name": "Bedroom Sensor", "archetype": "unknown_archetype" },
                    "product_data": { "model_id": "SML001", "product_name": "Hue motion sensor", "software_version": "1.0" },
                    "services": [
                        { "rid": "motion-002", "rtype": "motion" }
                    ]
                }
            ]
        }
        """;
        _mockHandler.ConfigureResponse(HttpStatusCode.OK, json);

        var result = await _client.GetDevicesAsync(TestAccessToken, TestApplicationKey);

        Assert.NotNull(result);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal("Kitchen Sensor", result.Data[0].Metadata.Name);
        Assert.Equal("Bedroom Sensor", result.Data[1].Metadata.Name);
    }

    [Fact]
    public async Task GetTemperatureSensorsAsync_ReturnsData()
    {
        var json = """
        {
            "errors": [],
            "data": [
                {
                    "id": "temp-001",
                    "type": "temperature",
                    "owner": { "rid": "dev-001", "rtype": "device" },
                    "enabled": true,
                    "temperature": {
                        "temperature_report": {
                            "temperature": 22.3,
                            "changed": "2026-02-27T14:00:00Z"
                        }
                    }
                }
            ]
        }
        """;
        _mockHandler.ConfigureResponse(HttpStatusCode.OK, json);

        var result = await _client.GetTemperatureSensorsAsync(TestAccessToken, TestApplicationKey);

        Assert.NotNull(result);
        Assert.Single(result.Data);
        Assert.Equal(22.3, result.Data[0].Temperature.TemperatureReport!.Temperature);
    }

    [Fact]
    public async Task RefreshTokenAsync_SendsBasicAuth()
    {
        var tokenResponse = new HueTokenResponse
        {
            AccessToken = "new-access",
            RefreshToken = "new-refresh",
            TokenType = "bearer",
            ExpiresIn = 604800
        };
        _mockHandler.ConfigureResponse(HttpStatusCode.OK, JsonSerializer.Serialize(tokenResponse, JsonOptions));

        await _client.RefreshTokenAsync(TestRefreshToken);

        Assert.NotNull(_mockHandler.CapturedRequest);
        Assert.Equal("Basic", _mockHandler.CapturedRequest!.Headers.Authorization?.Scheme);

        var expectedCredentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{TestClientId}:{TestClientSecret}"));
        Assert.Equal(expectedCredentials, _mockHandler.CapturedRequest.Headers.Authorization?.Parameter);

        Assert.Equal(HttpMethod.Post, _mockHandler.CapturedRequest.Method);
        Assert.Contains("oauth2/token", _mockHandler.CapturedRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsTokenResponse()
    {
        var json = """
        {
            "access_token": "refreshed-access-token",
            "refresh_token": "refreshed-refresh-token",
            "token_type": "bearer",
            "expires_in": 604800
        }
        """;
        _mockHandler.ConfigureResponse(HttpStatusCode.OK, json);

        var result = await _client.RefreshTokenAsync(TestRefreshToken);

        Assert.Equal("refreshed-access-token", result.AccessToken);
        Assert.Equal("refreshed-refresh-token", result.RefreshToken);
        Assert.Equal("bearer", result.TokenType);
        Assert.Equal(604800, result.ExpiresIn);
    }

    [Fact]
    public async Task GetMotionSensorsAsync_On401_ThrowsHttpRequestException()
    {
        _mockHandler.ConfigureResponse(HttpStatusCode.Unauthorized, """{"errors":[{"description":"unauthorized"}],"data":[]}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.GetMotionSensorsAsync(TestAccessToken, TestApplicationKey));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task GetMotionSensorsAsync_On429_ThrowsHttpRequestException()
    {
        _mockHandler.ConfigureResponse(HttpStatusCode.TooManyRequests, "Rate limited");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.GetMotionSensorsAsync(TestAccessToken, TestApplicationKey));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Fact]
    public async Task GetMotionSensorsAsync_On503_ThrowsHttpRequestException()
    {
        _mockHandler.ConfigureResponse(HttpStatusCode.ServiceUnavailable, "Service Unavailable");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.GetMotionSensorsAsync(TestAccessToken, TestApplicationKey));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
    }

    /// <summary>
    /// A mock HTTP message handler that captures the request and returns a configured response.
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private HttpStatusCode _statusCode = HttpStatusCode.OK;
        private string _responseBody = string.Empty;

        public HttpRequestMessage? CapturedRequest { get; private set; }

        public void ConfigureResponse(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;

            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
