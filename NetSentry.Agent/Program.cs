using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

// 1. НАСТРОЙКИ
string serverUrl = "http://192.***.*.**:5000/rmmHub"; 

Console.Title = "NetSentry AGENT [HIDDEN]";
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"INITIALIZING UPLINK TO {serverUrl}...");

var connection = new HubConnectionBuilder()
    .WithUrl(serverUrl)
    .WithAutomaticReconnect()
    .Build();

try
{
    await connection.StartAsync();
    Console.WriteLine(">> CONNECTION ESTABLISHED.");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"!! LINK ERROR: {ex.Message}");
    return;
}

// 2. ПОДГОТОВКА СЕНСОРОВ
var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
var ramCounter = new PerformanceCounter("Memory", "Available MBytes");
cpuCounter.NextValue(); 
await Task.Delay(1000);

// 3. БЕСКОНЕЧНЫЙ ЦИКЛ ОТПРАВКИ
while (true)
{
    try
    {
        // Данные о нагрузке
        float cpu = cpuCounter.NextValue();
        float ramFree = ramCounter.NextValue();

        // Данные о системе
        string machineName = Environment.MachineName;
        string userName = Environment.UserName;
        string osVersion = Environment.OSVersion.ToString(); 

        // Данные о диске C:\
        var drive = new DriveInfo("C");
        double diskTotalGb = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
        double diskFreeGb = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;

        // Отправляем всё пачкой на сервер
        await connection.InvokeAsync("SendFullMetrics",
            machineName,
            userName,
            osVersion,
            cpu,
            ramFree,
            diskTotalGb,
            diskFreeGb
        );

        Console.WriteLine($"[SENT] {machineName} | CPU: {cpu:F0}% | Disk Free: {diskFreeGb:F0} GB");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] {ex.Message}");
    }

    await Task.Delay(2000); 
}
