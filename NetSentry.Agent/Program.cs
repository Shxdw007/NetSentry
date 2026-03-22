using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Management;
using System.Text.Json;
using System.IO;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using LibreHardwareMonitor.Hardware; // <-- Теперь он на законном месте сверху!

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
Console.WriteLine("[INIT] Starting Temperature Sensors...");
var tempMonitor = new TemperatureMonitor(); // <-- Добавлена точка с запятой

while (true)
{
    try
    {
        // Сборка метрик
        float cpu = cpuCounter.NextValue();
        float ramFree = ramCounter.NextValue();

        // ЧИТАЕМ ГРАДУСЫ
        var temps = tempMonitor.GetTemperatures();
        float cpuTemp = temps.CpuTemp;
        float gpuTemp = temps.GpuTemp;

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

        // ОТПРАВЛЯЕМ НА ДАШБОРД
        await connection.InvokeAsync("SendUltraMetrics",
            machineName,
            userName,
            osVersion,
            cpu,
            ramFree,
            drivesJson,
            cpuName,
            gpuName,
            cpuTemp, 
            gpuTemp  
        );

        // Счётчик для БД
        dbSaveCounter++;

        if (dbSaveCounter >= 30)
        {
            dbSaveCounter = 0;
        }

        Console.Write($"\r[SEND] CPU:{cpu:00}% ({cpuTemp}°C) | RAM:{ramFree / 1024:F1}GB | GPU Temp: {gpuTemp}°C   ");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[ERROR] {ex.Message}");
    }

    await Task.Delay(1000);
}


// --- КЛАССЫ ---

// КЛАСС ДЛЯ РАБОТЫ С ЖЕЛЕЗОМ (Имена CPU и GPU)
public static class HardwareInfo
{
    public static string GetGpuInfo()
    {
        if (!OperatingSystem.IsWindows()) return "Non-Windows GPU";
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                return obj["Name"]?.ToString() ?? "Unknown";
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

// КЛАСС ДЛЯ ЧТЕНИЯ ТЕМПЕРАТУР
public class TemperatureMonitor
{
    private readonly Computer _computer;
    private bool _debugPrinted = false;

    public TemperatureMonitor()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true
        };
        _computer.Open();
    }

    public (float CpuTemp, float GpuTemp) GetTemperatures()
    {
        float currentCpuTemp = 0;
        float currentGpuTemp = 0;

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            CheckSensors(hardware, ref currentCpuTemp, ref currentGpuTemp);

            foreach (var subHardware in hardware.SubHardware)
            {
                subHardware.Update();
                CheckSensors(subHardware, ref currentCpuTemp, ref currentGpuTemp);
            }
        }

        //Если библиотека не нашла CPU (ноутбучный процессор) ===
        if (currentCpuTemp == 0)
        {
            currentCpuTemp = GetCpuTempFromWmiAcpi();
        }

        if (!_debugPrinted)
        {
            Console.WriteLine("\n[DEBUG] Датчики просканированы. Начинаю отправку...\n");
            _debugPrinted = true;
        }

        return (currentCpuTemp, currentGpuTemp);
    }

    private void CheckSensors(IHardware hardware, ref float currentCpuTemp, ref float currentGpuTemp)
    {
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
            {
                if (!_debugPrinted)
                {
                    Console.WriteLine($"[SENSOR DEBUG] {hardware.HardwareType} -> {sensor.Name}: {sensor.Value}°C");
                }

                if ((hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuAmd)
                    && sensor.Name.Contains("GPU Core"))
                {
                    currentGpuTemp = sensor.Value.Value;
                }
                else if (hardware.HardwareType == HardwareType.Cpu ||
                         hardware.HardwareType == HardwareType.Motherboard ||
                         hardware.HardwareType == HardwareType.SuperIO)
                {
                    if (sensor.Value.Value > currentCpuTemp && sensor.Value.Value < 120)
                    {
                        currentCpuTemp = sensor.Value.Value;
                    }
                }
            }
        }
    }

    //обходной путь через WMI ACPI
    private float GetCpuTempFromWmiAcpi()
    {
        try
        {
            // Обращаемся к тепловым зонам Windows
            var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
            foreach (var obj in searcher.Get())
            {
                // Windows хранит температуру в десятых долях Кельвина! Переводим в Цельсии:
                double tempKelvin = Convert.ToDouble(obj["CurrentTemperature"]) / 10.0;
                double tempCelsius = tempKelvin - 273.15;

                // Если температура похожа на правду (не статичная заглушка)
                if (tempCelsius > 20 && tempCelsius < 120)
                {
                    if (!_debugPrinted) Console.WriteLine($"[WMI ACPI DEBUG] Найдена тепловая зона: {tempCelsius:F1}°C");
                    return (float)tempCelsius;
                }
            }
        }
        catch
        {
        }

        return 0;
    }
}