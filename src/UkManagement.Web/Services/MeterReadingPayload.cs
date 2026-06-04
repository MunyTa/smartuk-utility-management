using System.Text.Json.Serialization;

namespace UkManagement.Web.Services;

public sealed class MeterReadingPayload
{
    [JsonPropertyName("deviceId")]
    public required string DeviceId { get; init; }

    [JsonPropertyName("value")]
    public decimal Value { get; init; }

    [JsonPropertyName("unit")]
    public required string Unit { get; init; }

    [JsonPropertyName("measuredAt")]
    public DateTimeOffset MeasuredAt { get; init; }

    [JsonPropertyName("signalRssi")]
    public int? SignalRssi { get; init; }

    [JsonPropertyName("batteryVoltage")]
    public decimal? BatteryVoltage { get; init; }
}
