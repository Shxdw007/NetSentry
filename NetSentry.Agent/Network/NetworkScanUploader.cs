using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using NetSentry.Agent.Models;

namespace NetSentry.Agent.Network;

public sealed class NetworkScanPostResult
{
    public bool Success { get; init; }
    public string RequestUrl { get; init; } = "";
    public HttpStatusCode? StatusCode { get; init; }
    public string? ResponseBody { get; init; }
    public string? ErrorMessage { get; init; }

    public string Describe()
    {
        if (Success)
            return $"OK {(int)StatusCode!} {StatusCode} -> {RequestUrl}";

        if (StatusCode.HasValue)
            return $"HTTP {(int)StatusCode} {StatusCode} -> {RequestUrl}" +
                   (string.IsNullOrWhiteSpace(ResponseBody) ? "" : $"\n  Body: {ResponseBody}");

        return $"{ErrorMessage} -> {RequestUrl}";
    }
}

public static class NetworkScanUploader
{
    public static async Task<NetworkScanPostResult> PostScanReportAsync(
        string apiBaseUrl,
        NetworkScanReportDto report,
        CancellationToken cancellationToken = default)
    {
        var baseUri = apiBaseUrl.TrimEnd('/');
        var url = $"{baseUri}/api/network/scan";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using var response = await client
                .PostAsync(url, report.ToJsonContent(), cancellationToken)
                .ConfigureAwait(false);

            string body = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (body.Length > 500)
                body = body[..500] + "...";

            return new NetworkScanPostResult
            {
                Success = response.IsSuccessStatusCode,
                RequestUrl = url,
                StatusCode = response.StatusCode,
                ResponseBody = body,
                ErrorMessage = response.IsSuccessStatusCode
                    ? null
                    : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
            };
        }
        catch (HttpRequestException ex)
        {
            return new NetworkScanPostResult
            {
                Success = false,
                RequestUrl = url,
                ErrorMessage = $"HttpRequestException: {ex.Message}" +
                               (ex.InnerException is not null ? $" | {ex.InnerException.Message}" : "")
            };
        }
        catch (TaskCanceledException ex)
        {
            return new NetworkScanPostResult
            {
                Success = false,
                RequestUrl = url,
                ErrorMessage = ex.CancellationToken.IsCancellationRequested
                    ? "Request cancelled (timeout)."
                    : $"TaskCanceledException: {ex.Message}"
            };
        }
        catch (SocketException ex)
        {
            return new NetworkScanPostResult
            {
                Success = false,
                RequestUrl = url,
                ErrorMessage = $"SocketException: {ex.Message} (Connection refused / host unreachable?)"
            };
        }
        catch (Exception ex)
        {
            return new NetworkScanPostResult
            {
                Success = false,
                RequestUrl = url,
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}"
            };
        }
    }
}
