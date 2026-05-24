using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace NetSentry.Dashboard.Services;

public sealed class NetworkDevicesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly string _apiBaseUrl;

    public NetworkDevicesService(string apiBaseUrl, string? authToken = null)
    {
        _apiBaseUrl = apiBaseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        if (!string.IsNullOrWhiteSpace(authToken))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
    }

    public async Task<List<NetworkDeviceDto>> FetchDevicesAsync(string agentName, CancellationToken cancellationToken = default)
    {
        string encodedName = Uri.EscapeDataString(agentName);
        string url = $"{_apiBaseUrl}/api/network/devices/{encodedName}";

        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var wrapper = JsonSerializer.Deserialize<ApiResponse<List<NetworkDeviceDto>>>(json, JsonOptions);

        if (wrapper?.Success == true && wrapper.Data is not null)
            return wrapper.Data;

        return [];
    }

    private sealed class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }
}
