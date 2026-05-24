using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetSentry.Agent.Models;

namespace NetSentry.Agent.Network;

/// <summary>
/// Быстрый асинхронный ping sweep локальной IPv4-подсети.
/// </summary>
public sealed class NetworkScanner
{
    private readonly int _timeoutMs;
    private readonly int _maxHosts;

    public NetworkScanner(int timeoutMilliseconds = 250, int maxHosts = 1022)
    {
        _timeoutMs = timeoutMilliseconds;
        _maxHosts = maxHosts;
    }

    public string? LocalInterfaceIp { get; private set; }
    public string? SubnetCidr { get; private set; }

    /// <summary>
    /// Определяет подсеть машины и сканирует все хосты (кроме network/broadcast).
    /// </summary>
    public async Task<List<string>> ScanLocalSubnetAsync(CancellationToken cancellationToken = default)
    {
        var subnet = DetectLocalIpv4Subnet();
        if (subnet is null)
            return [];

        LocalInterfaceIp = subnet.LocalIp.ToString();
        SubnetCidr = subnet.Cidr;

        var hosts = EnumerateHostAddresses(subnet.NetworkAddress, subnet.SubnetMask).ToList();
        return await ScanHostsAsync(hosts, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Сканирует явный диапазон (например 192.168.1.1 — 192.168.1.254).
    /// </summary>
    public async Task<List<string>> ScanRangeAsync(
        IPAddress startInclusive,
        IPAddress endInclusive,
        CancellationToken cancellationToken = default)
    {
        var hosts = EnumerateRange(startInclusive, endInclusive).ToList();
        return await ScanHostsAsync(hosts, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(List<string> ActiveHosts, int DurationMs)> ScanLocalSubnetWithTimingAsync(
        CancellationToken cancellationToken = default)
    {
        var (hosts, ms) = await ScanLocalSubnetWithHostsAsync(cancellationToken).ConfigureAwait(false);
        return (hosts.Select(h => h.IpAddress).ToList(), ms);
    }

    /// <summary>
    /// Ping sweep + MAC из ARP-таблицы для каждого активного IP.
    /// </summary>
    public async Task<(List<NetworkHostDto> Hosts, int DurationMs)> ScanLocalSubnetWithHostsAsync(
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var activeIps = await ScanLocalSubnetAsync(cancellationToken).ConfigureAwait(false);
        var macTable = await ArpResolver.ResolveMacAddressesAsync(activeIps, cancellationToken)
            .ConfigureAwait(false);

        var hosts = activeIps.Select(ip => new NetworkHostDto
        {
            IpAddress = ip,
            MacAddress = macTable.TryGetValue(ip, out var mac) ? mac : ""
        }).ToList();

        sw.Stop();
        return (hosts, (int)sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Выбирает активный IPv4-интерфейс с шлюзом (Ethernet/Wi‑Fi), иначе первый подходящий.
    /// </summary>
    public static LocalSubnetInfo? DetectLocalIpv4Subnet()
    {
        var candidates = new List<(NetworkInterface Ni, UnicastIPAddressInformation Uni)>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;

            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel)
                continue;

            var ipProps = ni.GetIPProperties();
            bool hasGateway = ipProps.GatewayAddresses
                .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                          && !g.Address.Equals(IPAddress.Any));

            foreach (var uni in ipProps.UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                if (!IsUsableHostAddress(uni.Address, uni.IPv4Mask))
                    continue;

                candidates.Add((ni, uni));
            }
        }

        var chosen = candidates
            .OrderByDescending(c => c.Ni.GetIPProperties().GatewayAddresses
                .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
            .ThenByDescending(c => c.Ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            .ThenByDescending(c => c.Ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .FirstOrDefault();

        if (chosen.Ni is null || chosen.Uni.IPv4Mask is null)
            return null;

        var ip = chosen.Uni.Address;
        var mask = chosen.Uni.IPv4Mask;
        var network = ApplyMask(ip, mask);
        int prefixLength = MaskToPrefixLength(mask);

        return new LocalSubnetInfo(
            ip,
            network,
            mask,
            prefixLength,
            $"{network}/{prefixLength}",
            chosen.Ni.Name);
    }

    private async Task<List<string>> ScanHostsAsync(
        IReadOnlyList<IPAddress> hosts,
        CancellationToken cancellationToken)
    {
        if (hosts.Count == 0)
            return [];

        if (hosts.Count > _maxHosts)
            throw new InvalidOperationException(
                $"Подсеть содержит {hosts.Count} хостов (лимит {_maxHosts}). Укажите меньшую маску или ScanRangeAsync.");

        var tasks = hosts.Select(ip => PingHostAsync(ip, cancellationToken)).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        return results
            .Where(ip => ip is not null)
            .Select(ip => ip!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ip => ip, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<string?> PingHostAsync(IPAddress address, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, _timeoutMs)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            return reply.Status == IPStatus.Success ? address.ToString() : null;
        }
        catch (PingException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<IPAddress> EnumerateHostAddresses(IPAddress network, IPAddress mask)
    {
        uint net = ToUInt32(network);
        uint maskBits = ToUInt32(mask);
        uint hostMin = (net & maskBits) + 1;
        uint hostMax = (net | ~maskBits) - 1;

        if (hostMax < hostMin)
            yield break;

        for (uint host = hostMin; host <= hostMax; host++)
            yield return FromUInt32(host);
    }

    private static IEnumerable<IPAddress> EnumerateRange(IPAddress start, IPAddress end)
    {
        uint from = ToUInt32(start);
        uint to = ToUInt32(end);
        if (from > to)
            (from, to) = (to, from);

        for (uint i = from; i <= to; i++)
            yield return FromUInt32(i);
    }

    private static bool IsUsableHostAddress(IPAddress ip, IPAddress? mask)
    {
        if (mask is null)
            return false;

        if (IPAddress.IsLoopback(ip))
            return false;

        var bytes = ip.GetAddressBytes();
        // APIPA 169.254.x.x
        if (bytes[0] == 169 && bytes[1] == 254)
            return false;

        return true;
    }

    private static IPAddress ApplyMask(IPAddress ip, IPAddress mask)
    {
        uint ipBits = ToUInt32(ip);
        uint maskBits = ToUInt32(mask);
        return FromUInt32(ipBits & maskBits);
    }

    private static int MaskToPrefixLength(IPAddress mask)
    {
        uint bits = ToUInt32(mask);
        int count = 0;
        while (bits != 0)
        {
            count += (int)(bits & 1);
            bits >>= 1;
        }
        return count;
    }

    private static uint ToUInt32(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static IPAddress FromUInt32(uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return new IPAddress(bytes);
    }
}

public sealed record LocalSubnetInfo(
    IPAddress LocalIp,
    IPAddress NetworkAddress,
    IPAddress SubnetMask,
    int PrefixLength,
    string Cidr,
    string InterfaceName);
