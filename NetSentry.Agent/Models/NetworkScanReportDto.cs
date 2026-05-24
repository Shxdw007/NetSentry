using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetSentry.Agent.Models;

/// <summary>
/// Отчёт ping sweep + MAC для POST на сервер (POST /api/network/scan).
/// </summary>
public sealed class NetworkScanReportDto
{
    [JsonPropertyName("agentMachineName")]
    public string AgentMachineName { get; init; } = "";

    [JsonPropertyName("scannerIp")]
    public string ScannerIp { get; init; } = "";

    [JsonPropertyName("subnetCidr")]
    public string SubnetCidr { get; init; } = "";

    [JsonPropertyName("scannedAtUtc")]
    public DateTime ScannedAtUtc { get; init; }

    [JsonPropertyName("scanDurationMs")]
    public int ScanDurationMs { get; init; }

    [JsonPropertyName("activeHosts")]
    public List<NetworkHostDto> ActiveHosts { get; init; } = [];

    public static NetworkScanReportDto Create(
        string machineName,
        string scannerIp,
        string subnetCidr,
        IReadOnlyList<NetworkHostDto> activeHosts,
        int scanDurationMs)
    {
        return new NetworkScanReportDto
        {
            AgentMachineName = machineName,
            ScannerIp = scannerIp,
            SubnetCidr = subnetCidr,
            ScannedAtUtc = DateTime.UtcNow,
            ScanDurationMs = scanDurationMs,
            ActiveHosts = activeHosts
                .OrderBy(h => h.IpAddress, StringComparer.Ordinal)
                .ToList()
        };
    }

    public string ToJson() =>
        JsonSerializer.Serialize(this, AgentJsonOptions.Default);

    public StringContent ToJsonContent() =>
        new(ToJson(), System.Text.Encoding.UTF8, "application/json");
}

internal static class AgentJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
