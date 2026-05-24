using NetSentry.Agent.Models;

namespace NetSentry.Agent.Network;

public static class NetworkScanConsole
{
    public static bool HasValidMac(NetworkHostDto host) =>
        !string.IsNullOrWhiteSpace(host.MacAddress)
        && !host.MacAddress.Equals("?", StringComparison.Ordinal)
        && host.MacAddress != "00:00:00:00:00:00";

    /// <summary>
    /// Выводит только хосты с известным MAC. Хосты без MAC в консоль не попадают.
    /// </summary>
    public static void PrintScanResults(string? subnetCidr, int durationMs, IReadOnlyList<NetworkHostDto> hosts)
    {
        var identified = hosts
            .Where(HasValidMac)
            .OrderBy(h => h.IpAddress, StringComparer.Ordinal)
            .ToList();

        if (identified.Count == 0)
            return;

        Console.WriteLine();
        string subnet = string.IsNullOrWhiteSpace(subnetCidr) ? "LAN" : subnetCidr;
        Console.WriteLine($"[NET] Сканирование {subnet} — {identified.Count} устройств ({durationMs} ms)");

        foreach (var host in identified)
        {
            Console.WriteLine(
                $"[NET] 🟢 IP: {host.IpAddress}  |  MAC: {host.MacAddress}  |  Устройство активно");
        }
    }

    public static void PrintApiSuccess() =>
        AgentConsole.DebugLine("[NET] Отчёт отправлен на сервер.");

    public static void PrintApiFailure(string details)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[NET] Ошибка отправки: {details}");
        Console.ForegroundColor = ConsoleColor.Cyan;
    }
}
