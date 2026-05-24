using System.Text.Json.Serialization;

namespace NetSentry.Agent.Models;

public sealed class NetworkHostDto
{
    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; init; } = "";

    [JsonPropertyName("macAddress")]
    public string MacAddress { get; init; } = "";
}
