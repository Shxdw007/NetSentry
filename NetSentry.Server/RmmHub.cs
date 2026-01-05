using Microsoft.AspNetCore.SignalR;

public class RmmHub : Hub
{
    public async Task SendFullMetrics(
        string machineName,
        string userName,
        string osVersion,
        double cpu,
        double ramFree,
        double diskTotal,
        double diskFree)
    {
        await Clients.All.SendAsync("ReceiveFullMetrics",
            machineName, userName, osVersion, cpu, ramFree, diskTotal, diskFree);

        Console.WriteLine($"[DATA] {machineName} updated.");
    }
}
