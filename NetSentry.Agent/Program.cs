using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Management;
using System.Text.Json;


// НАСТРОЙКИ
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

string serverUrl = config["ServerUrl"] ?? "http://localhost:5000/rmmHub";

Console.Title = "NetSentry AGENT [v2.1]";
Console.ForegroundColor = ConsoleColor.Cyan;

// Инициализация WMI (проверка железа)
Console.WriteLine("[INIT] Scanning Hardware...");
string cpuName = HardwareInfo.GetCpuName();
string gpuName = HardwareInfo.GetGpuInfo();
Console.WriteLine($"   > CPU: {cpuName}");
Console.WriteLine($"   > GPU: {gpuName}");

var connection = new HubConnectionBuilder()
    .WithUrl(serverUrl)
    .WithAutomaticReconnect()
    .Build();

try
{
    Console.WriteLine($"[LINK] Connecting to {serverUrl}...");
    await connection.StartAsync();
    Console.WriteLine("[LINK] CONNECTED!");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[ERROR] {ex.Message}");
    return;
}

// Подготовка счётчиков
var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
var ramCounter = new PerformanceCounter("Memory", "Available MBytes");
cpuCounter.NextValue();
await Task.Delay(1000);

// Счётчик для сохранения в БД (каждые 30 секунд)
int dbSaveCounter = 0;

while (true)
{
    try
    {
        // Сборка метрик
        float cpu = cpuCounter.NextValue();
        float ramFree = ramCounter.NextValue();

        string machineName = Environment.MachineName;
        string userName = Environment.UserName;
        string osVersion = Environment.OSVersion.ToString();

        // Сборка всех дисков
        var drivesList = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new
            {
                DriveName = d.Name,  
                TotalSizeGb = d.TotalSize / 1024.0 / 1024.0 / 1024.0,
                FreeSizeGb = d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0
            })
            .ToList();

        string drivesJson = JsonSerializer.Serialize(drivesList);

        // ОТПРАВЛЯЕМ НА ДАШБОРД (каждую секунду)
        await connection.InvokeAsync("SendUltraMetrics",
            machineName,
            userName,
            osVersion,
            cpu,
            ramFree,
            drivesJson,
            gpuName,
            cpuName
        );

        // Счётчик для БД
        dbSaveCounter++;

        if (dbSaveCounter >= 30)
        {
            Console.WriteLine($"[DB SAVE] Сохранено в БД: CPU:{cpu:00}% | RAM:{ramFree / 1024:F1}GB | Drives:{drivesList.Count}");
            dbSaveCounter = 0;
        }

        Console.Write($"\r[SEND] CPU:{cpu:00}% | RAM:{ramFree / 1024:F1}GB | DRIVES:{drivesList.Count} | GPU OK   ");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[ERROR] {ex.Message}");
    }

    await Task.Delay(1000);
}


//КЛАСС ДЛЯ РАБОТЫ С ЖЕЛЕЗОМ
public static class HardwareInfo
{
    public static string GetGpuInfo()
    {
        if (!OperatingSystem.IsWindows()) return "Non-Windows GPU";
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? "Unknown";
                long vram = 0;
                try { vram = Convert.ToInt64(obj["AdapterRAM"]); } catch { }
                double vramGb = vram / 1024.0 / 1024.0 / 1024.0;
                return $"{name} ({vramGb:F1} GB)";
            }
        }
        catch { return "GPU Error"; }
        return "No GPU";
    }

    public static string GetCpuName()
    {
        if (!OperatingSystem.IsWindows()) return "Non-Windows CPU";
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
                return obj["Name"]?.ToString()?.Trim() ?? "Unknown";
        }
        catch { return "CPU Error"; }
        return "Unknown";
    }
}
