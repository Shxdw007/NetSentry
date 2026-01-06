using Microsoft.AspNetCore.SignalR;

public class RmmHub : Hub
{
    // Новый метод, который принимает полный фарш данных
    public async Task SendUltraMetrics(
        string machineName,
        string userName,
        string osVersion,
        double cpu,
        double ramFree,
        string drivesJson,
        string cpuName,    
        string gpuName     
        )
    {
        Console.WriteLine($"[DATA] {machineName} | CPU: {cpu:F0}% | Drives: JSON size {drivesJson.Length}");

        await Clients.All.SendAsync("ReceiveUltraMetrics",
            machineName,
            userName,
            osVersion,
            cpu,
            ramFree,
            drivesJson,
            cpuName,
            gpuName
        );
    }
}
