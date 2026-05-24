using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace NetSentry.Agent.Network;

/// <summary>
/// Разрешение MAC по ARP-таблице Windows (arp -a). Парсер не зависит от языка ОС.
/// </summary>
public static partial class ArpResolver
{
    // MAC: aa-bb-cc-dd-ee-ff или aa:bb:cc:dd:ee:ff
    private static readonly Regex MacRegex = MacAddressPattern();

    // IPv4 на строке
    private static readonly Regex IpRegex = IPv4Pattern();

    public static async Task<IReadOnlyDictionary<string, string>> ResolveMacAddressesAsync(
        IEnumerable<string> ipAddresses,
        CancellationToken cancellationToken = default)
    {
        var ips = ipAddresses
            .Where(ip => !string.IsNullOrWhiteSpace(ip))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ips.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await Task.Delay(150, cancellationToken).ConfigureAwait(false);

        var table = ReadArpTable();
        var missing = ips.Where(ip => !table.ContainsKey(ip)).ToList();

        if (missing.Count > 0)
        {
            await PingHostsAsync(missing, cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            foreach (var (ip, mac) in ReadArpTable())
                table[ip] = mac;
        }

        return table;
    }

    private static async Task PingHostsAsync(IReadOnlyList<string> ips, CancellationToken cancellationToken)
    {
        var tasks = ips.Select(async ip =>
        {
            try
            {
                if (!IPAddress.TryParse(ip, out var address))
                    return;

                using var ping = new System.Net.NetworkInformation.Ping();
                await ping.SendPingAsync(address, 150)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static Dictionary<string, string> ReadArpTable()
    {
        string? output = RunArpCommand();
        if (string.IsNullOrWhiteSpace(output))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return ParseArpOutput(output);
    }

    internal static Dictionary<string, string> ParseArpOutput(string output)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var ipMatch = IpRegex.Match(line);
            var macMatch = MacRegex.Match(line);

            if (!ipMatch.Success || !macMatch.Success)
                continue;

            string ip = ipMatch.Groups["ip"].Value;
            if (!IsHostIp(ip))
                continue;

            string mac = NormalizeMac(macMatch.Groups["mac"].Value);
            if (string.IsNullOrEmpty(mac) || mac == "00:00:00:00:00:00")
                continue;

            map[ip] = mac;
        }

        return map;
    }

    private static string? RunArpCommand()
    {
        string? best = null;
        int bestCount = 0;

        // Windows: arp часто в OEM (CP866) или Windows-1251 при русской локали
        foreach (var encoding in GetConsoleEncodings())
        {
            try
            {
                string text = RunArpWithEncoding(encoding);
                int count = ParseArpOutput(text).Count;
                if (count > bestCount)
                {
                    bestCount = count;
                    best = text;
                }
            }
            catch
            {
                // try next encoding
            }
        }

        return best ?? RunArpWithEncoding(Encoding.UTF8);
    }

    private static Encoding[] GetConsoleEncodings()
    {
        var list = new List<Encoding> { Encoding.UTF8, Encoding.Default };
        try { list.Add(Encoding.GetEncoding(866)); } catch { }
        try { list.Add(Encoding.GetEncoding(1251)); } catch { }
        return list.Distinct().ToArray();
    }

    private static string RunArpWithEncoding(Encoding encoding)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "arp",
            Arguments = "-a",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Failed to start arp.exe");

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        return output;
    }

    private static bool IsHostIp(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr))
            return false;

        if (IPAddress.IsLoopback(addr))
            return false;

        var b = addr.GetAddressBytes();
        // broadcast / multicast
        if (b[3] == 255 || (b[0] >= 224 && b[0] <= 239))
            return false;

        return true;
    }

    public static string NormalizeMac(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var hex = raw.Replace("-", "", StringComparison.Ordinal)
            .Replace(":", "", StringComparison.Ordinal)
            .Replace(".", "", StringComparison.Ordinal)
            .Trim();

        if (hex.Length != 12)
            return "";

        return string.Join(':',
            Enumerable.Range(0, 6)
                .Select(i => hex.Substring(i * 2, 2))
                .Select(pair => pair.ToUpperInvariant()));
    }

    [GeneratedRegex(@"(?<mac>(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2})", RegexOptions.Compiled)]
    private static partial Regex MacAddressPattern();

    [GeneratedRegex(@"\b(?<ip>(?:\d{1,3}\.){3}\d{1,3})\b", RegexOptions.Compiled)]
    private static partial Regex IPv4Pattern();
}
