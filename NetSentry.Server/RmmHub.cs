using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NetSentry.Server.Data;
using NetSentry.Server.Models;

public class RmmHub : Hub
{
    private readonly AppDbContext _db;


    // 1. Словарь для связки: ConnectionId -> Имя машины (Решает проблему "вечного онлайна")
    private static readonly ConcurrentDictionary<string, string> _connectedMachines = new();

    // 2. Словарь для умного сохранения в БД раз в 30 секунд
    private static readonly ConcurrentDictionary<string, DateTime> _lastDbSave = new(); public RmmHub(AppDbContext db)
    {
        _db = db;
    }

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"[CONNECT] Client connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    // Правильное точечное отключение
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"[DISCONNECT] Client disconnected: {Context.ConnectionId}");

        // Достаем имя ПК по его ID подключения и сразу удаляем из словаря
        if (_connectedMachines.TryRemove(Context.ConnectionId, out string disconnectedMachineName))
        {
            // Ищем только одну конкретную машину в БД
            var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Name == disconnectedMachineName);
            if (machine != null)
            {
                machine.Status = "Offline";
                await _db.SaveChangesAsync();

                // Уведомляем дашборд, что конкретно этот ПК отвалился
                await Clients.All.SendAsync("MachineDisconnected", disconnectedMachineName);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public class DriveInfoDto
    {
        public string DriveName { get; set; } = null!;
        public double TotalSizeGb { get; set; }
        public double FreeSizeGb { get; set; }
    }

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
        // Привязываем текущее подключение к имени машины (если еще не привязано)
        _connectedMachines.TryAdd(Context.ConnectionId, machineName);

        // 1. Находим или создаём запись о машине
        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Name == machineName);
        bool statusChanged = false; // Флаг для экономии запросов к БД

        if (machine == null)
        {
            machine = new Machine
            {
                Name = machineName,
                CpuName = cpuName,
                GpuName = gpuName,
                OsVersion = osVersion,
                Status = "Online",
                FirstConnected = DateTime.UtcNow,
                LastConnected = DateTime.UtcNow
            };

            _db.Machines.Add(machine);
            statusChanged = true;
            await Clients.All.SendAsync("MachineConnected", machineName);
        }
        else
        {
            // Обновляем статус только если он был Offline, чтобы не спамить БД каждую секунду
            if (machine.Status != "Online")
            {
                machine.Status = "Online";
                statusChanged = true;
                await Clients.All.SendAsync("MachineReconnected", machineName);
            }

            machine.CpuName = cpuName;
            machine.GpuName = gpuName;
            machine.OsVersion = osVersion;
            machine.LastConnected = DateTime.UtcNow;
        }

        // 2. Сохраняем метрику
        // Проверяем, прошло ли 30 секунд с момента последней записи для этого ПК
        bool shouldSaveToDb = false;
        if (!_lastDbSave.TryGetValue(machineName, out var lastSaveTime) || (DateTime.UtcNow - lastSaveTime).TotalSeconds >= 30)
        {
            shouldSaveToDb = true;
            _lastDbSave[machineName] = DateTime.UtcNow; // Обновляем таймер
        }

        if (shouldSaveToDb)
        {
            var metric = new Metric
            {
                Machine = machine,
                CpuUsage = (float)cpu,
                RamFree = (float)ramFree,
                Timestamp = DateTime.UtcNow
            };
            _db.Metrics.Add(metric);
            // Диски тоже можно обернуть в этот if(shouldSaveToDb), чтобы не проверять их каждую секунду
        }
        // ----------------------------------------

        if (shouldSaveToDb || statusChanged)
        {
            await _db.SaveChangesAsync();
        }

        // 4. Рассылаем данные на дашборд ВСЕГДА (каждую секунду для красивой анимации)
        await Clients.All.SendAsync("ReceiveUltraMetrics", machineName, userName, osVersion, cpu, ramFree, drivesJson, cpuName, gpuName);

        // 3. Обновляем информацию по дискам
        if (!string.IsNullOrWhiteSpace(drivesJson))
        {
            try
            {
                var drives = JsonSerializer.Deserialize<List<DriveInfoDto>>(drivesJson);
                if (drives != null)
                {
                    foreach (var d in drives)
                    {
                        if (string.IsNullOrWhiteSpace(d.DriveName)) continue;

                        var disk = await _db.Disks.FirstOrDefaultAsync(x => x.MachineId == machine.Id && x.DriveName == d.DriveName);

                        if (disk == null)
                        {
                            disk = new Disk { Machine = machine, DriveName = d.DriveName, TotalSizeGb = d.TotalSizeGb, FreeSizeGb = d.FreeSizeGb };
                            _db.Disks.Add(disk);
                        }
                        else
                        {
                            disk.TotalSizeGb = d.TotalSizeGb;
                            disk.FreeSizeGb = d.FreeSizeGb;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Cannot parse drivesJson: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync();

        // 4. Рассылаем данные на дашборд
        await Clients.All.SendAsync("ReceiveUltraMetrics", machineName, userName, osVersion, cpu, ramFree, drivesJson, cpuName, gpuName);
    }
}