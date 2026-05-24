using LibreHardwareMonitor.Hardware;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using NetSentry.Agent;
using NetSentry.Agent.Models;
using NetSentry.Agent.Network;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LhmPawnIo = LibreHardwareMonitor.PawnIo.PawnIo;

Console.OutputEncoding = Encoding.UTF8;
// НАСТРОЙКИ
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

string serverUrl = config["ServerUrl"] ?? "http://localhost:5000/rmmHub";
string? apiUrl = config["ApiUrl"];
int networkScanIntervalMinutes = int.TryParse(config["NetworkScanIntervalMinutes"], out int scanMin) && scanMin > 0
    ? scanMin
    : 15;

AgentConsole.IsDebug = bool.TryParse(config["Debug"], out bool debugFlag) && debugFlag;

Console.Title = "NetSentry AGENT [v2.3]";
Console.ForegroundColor = ConsoleColor.Cyan;

// Инициализация WMI (проверка железа)
AgentConsole.InitLine("[INIT] Scanning Hardware...");
string cpuName = HardwareInfo.GetCpuName();
string gpuName = HardwareInfo.GetGpuInfo();
AgentConsole.InitLine($"   > CPU: {cpuName}");
AgentConsole.InitLine($"   > GPU: {gpuName}");

if (!AgentPrivileges.IsAdministrator())
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("[WARN] Агент запущен БЕЗ прав администратора.");
    Console.WriteLine("[WARN] Запустите агент от имени администратора или проверьте app.manifest (requireAdministrator).");
    Console.ForegroundColor = ConsoleColor.Cyan;
}

PawnIoDiagnostics.PrintStatus();

var connection = new HubConnectionBuilder()
    .WithUrl(serverUrl)
    .WithAutomaticReconnect()
    .Build();

try
{
    AgentConsole.InitLine($"[LINK] Connecting to {serverUrl}...");
    await connection.StartAsync();
    AgentConsole.InitLine("[LINK] CONNECTED!");
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
AgentConsole.InitLine("[INIT] Starting Temperature Sensors...");
var tempMonitor = new TemperatureMonitor();

var networkScanner = new NetworkScanner(timeoutMilliseconds: 250);
DateTime lastNetworkScanUtc = DateTime.MinValue;

await RunNetworkScanAsync(networkScanner, apiUrl);

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

        if ((DateTime.UtcNow - lastNetworkScanUtc).TotalMinutes >= networkScanIntervalMinutes)
        {
            lastNetworkScanUtc = DateTime.UtcNow;
            _ = RunNetworkScanAsync(networkScanner, apiUrl);
        }

        Console.Write($"\r[SEND] CPU:{cpu:00}% ({cpuTemp}°C) | RAM:{ramFree / 1024:F1}GB | GPU Temp: {gpuTemp}°C   ");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[ERROR] {ex.Message}");
    }

    await Task.Delay(1000);
}

static async Task RunNetworkScanAsync(NetworkScanner scanner, string? apiUrl)
{
    try
    {
        var (activeHosts, durationMs) = await scanner.ScanLocalSubnetWithHostsAsync();
        var report = NetworkScanReportDto.Create(
            Environment.MachineName,
            scanner.LocalInterfaceIp ?? "",
            scanner.SubnetCidr ?? "",
            activeHosts,
            durationMs);

        NetworkScanConsole.PrintScanResults(report.SubnetCidr, durationMs, activeHosts);

        if (!string.IsNullOrWhiteSpace(apiUrl))
        {
            var postResult = await NetworkScanUploader.PostScanReportAsync(apiUrl, report);
            if (postResult.Success)
                NetworkScanConsole.PrintApiSuccess();
            else
                NetworkScanConsole.PrintApiFailure(postResult.Describe());
        }
        else
        {
            AgentConsole.DebugLine("[NET] ApiUrl не задан — отчёт не отправлен на сервер.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[NET] Scan error: {ex.Message}");
    }
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

public static class PawnIoDiagnostics
{
    public static void PrintStatus()
    {
        try
        {
            bool installed = LhmPawnIo.IsInstalled;
            string version = LhmPawnIo.Version?.ToString() ?? "n/a";
            AgentConsole.InitLine($"[INIT] PawnIO: installed={installed}, version={version}");

            if (!installed)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[WARN] AMD Ryzen (LHM 0.9+) требует драйвер PawnIO. GPU может работать, CPU temp = 0 без него.");
                Console.WriteLine("[WARN] Решение: установи LibreHardwareMonitor → согласись на установку PawnIO → перезагрузка ПК.");
                Console.WriteLine("[WARN] Либо PawnIO Setup: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor");
                Console.ForegroundColor = ConsoleColor.Cyan;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Не удалось проверить PawnIO: {ex.Message}");
        }
    }
}

public static class AgentPrivileges
{
    public static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}

// КЛАСС ДЛЯ ЧТЕНИЯ ТЕМПЕРАТУР (LibreHardwareMonitor)
public class TemperatureMonitor : IDisposable
{
    private readonly Computer _computer;
    private bool _debugPrinted;

    public TemperatureMonitor()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = false,
            IsMemoryEnabled = false,
            IsStorageEnabled = false,
            IsNetworkEnabled = false,
            IsPsuEnabled = false
        };
        _computer.Open();

        // Даём LHM время инициализировать CPU/PawnIO перед первым чтением
        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
                sub.Update();
        }
    }

    public (float CpuTemp, float GpuTemp) GetTemperatures()
    {
        float cpuTemp = CollectMaxCpuTemperature(useAmdFilter: true);

        if (cpuTemp <= 0)
            cpuTemp = CollectMaxCpuTemperature(useAmdFilter: false);

        if (cpuTemp <= 0)
            cpuTemp = CollectCpuTempFromMotherboard();

        float gpuTemp = CollectMaxGpuTemperature();

        if (cpuTemp <= 0)
            cpuTemp = GetCpuTempFromWmiAcpi();

        if (!_debugPrinted)
        {
            if (AgentConsole.IsDebug)
            {
                Console.WriteLine($"\n[DEBUG] CPU temp: {cpuTemp}°C | GPU temp: {gpuTemp}°C | Admin: {AgentPrivileges.IsAdministrator()}");
                if (cpuTemp <= 0)
                    DumpAllTemperatureSensors();
                Console.WriteLine();
            }
            _debugPrinted = true;
        }

        return (cpuTemp, gpuTemp);
    }

    private float CollectMaxCpuTemperature(bool useAmdFilter)
    {
        float maxTemp = 0f;

        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Cpu)
                continue;

            maxTemp = Math.Max(maxTemp, ReadMaxCpuTempFromHardware(hardware, useAmdFilter));

            foreach (var subHardware in hardware.SubHardware)
                maxTemp = Math.Max(maxTemp, ReadMaxCpuTempFromHardware(subHardware, useAmdFilter));
        }

        return maxTemp;
    }

    private float ReadMaxCpuTempFromHardware(IHardware hardware, bool useAmdFilter)
    {
        hardware.Update();

        float max = 0f;
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Temperature)
                continue;

            if (!sensor.Value.HasValue)
            {
                LogSensorOnce(hardware, sensor, "(нет значения)");
                continue;
            }

            float value = sensor.Value.Value;
            if (value <= 0f || value >= 120f)
                continue;

            if (useAmdFilter && !IsRelevantCpuTemperatureSensor(sensor.Name))
                continue;

            LogSensorOnce(hardware, sensor);
            if (value > max)
                max = value;
        }

        return max;
    }

    private float CollectCpuTempFromMotherboard()
    {
        float max = 0f;

        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType is not (HardwareType.Motherboard or HardwareType.SuperIO))
                continue;

            max = Math.Max(max, ReadMotherboardCpuTemp(hardware));
            foreach (var sub in hardware.SubHardware)
                max = Math.Max(max, ReadMotherboardCpuTemp(sub));
        }

        return max;
    }

    private float ReadMotherboardCpuTemp(IHardware hardware)
    {
        hardware.Update();
        float max = 0f;

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue)
                continue;

            float value = sensor.Value.Value;
            if (value <= 0f || value >= 120f)
                continue;

            if (!sensor.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
                continue;

            if (value > max)
                max = value;
        }

        return max;
    }

    private void DumpAllTemperatureSensors()
    {
        if (!AgentConsole.IsDebug)
            return;

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("[DEBUG] CPU=0 — все Temperature-сенсоры LHM (ищи PawnIO если везде 'нет значения'):");
        foreach (var hardware in _computer.Hardware)
        {
            DumpHardwareSensors(hardware);
            foreach (var sub in hardware.SubHardware)
                DumpHardwareSensors(sub);
        }
        Console.ForegroundColor = ConsoleColor.Cyan;
    }

    private static void DumpHardwareSensors(IHardware hardware)
    {
        hardware.Update();
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Temperature)
                continue;

            string value = sensor.Value.HasValue ? $"{sensor.Value:F1}°C" : "нет значения";
            Console.WriteLine($"  {hardware.HardwareType} | {hardware.Name} | {sensor.Name} = {value}");
        }
    }

    /// <summary>
    /// AMD Ryzen: Tctl/Tdie, Tdie, Core, Package. Intel: Package / Core тоже подходят.
    /// </summary>
    private static bool IsRelevantCpuTemperatureSensor(string sensorName)
    {
        if (string.IsNullOrWhiteSpace(sensorName))
            return false;

        if (sensorName.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
            || sensorName.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
            || sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase))
            return true;

        if (sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase))
            return true;

        // Запасной вариант для Intel и экзотических раскладок LHM
        if (sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
            && sensorName.Contains("Temperature", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private float CollectMaxGpuTemperature()
    {
        float maxTemp = 0f;

        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType is not (HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel))
                continue;

            hardware.Update();
            maxTemp = Math.Max(maxTemp, ReadMaxGpuTempFromHardware(hardware));

            foreach (var subHardware in hardware.SubHardware)
            {
                subHardware.Update();
                maxTemp = Math.Max(maxTemp, ReadMaxGpuTempFromHardware(subHardware));
            }
        }

        return maxTemp;
    }

    private float ReadMaxGpuTempFromHardware(IHardware hardware)
    {
        float max = 0f;
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue)
                continue;

            float value = sensor.Value.Value;
            if (value <= 0f || value >= 120f)
                continue;

            if (!sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                && !sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                continue;

            if (value > max)
                max = value;
        }

        return max;
    }

    private void LogSensorOnce(IHardware hardware, ISensor sensor, string? overrideValue = null)
    {
        if (_debugPrinted || !AgentConsole.IsDebug)
            return;

        string valueText = overrideValue ?? (sensor.Value.HasValue ? $"{sensor.Value:F1}°C" : "нет значения");
        Console.WriteLine($"[SENSOR DEBUG] {hardware.HardwareType} / {hardware.Name} -> {sensor.Name}: {valueText}");
    }

    public void Dispose() => _computer.Close();

    // Обходной путь через WMI ACPI (ноутбуки / когда LHM не видит CPU)
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
                    if (!_debugPrinted && AgentConsole.IsDebug)
                        Console.WriteLine($"[WMI ACPI DEBUG] Найдена тепловая зона: {tempCelsius:F1}°C");
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