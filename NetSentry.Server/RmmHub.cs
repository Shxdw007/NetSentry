using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NetSentry.Server.Data;
using NetSentry.Server.Models;

public class RmmHub : Hub
{
    private readonly AppDbContext _db;

    public RmmHub(AppDbContext db)
    {
        _db = db;
    }

    // Модель для одного диска, чтобы распарсить JSON от агента
    public class DriveInfoDto
    {
        public string DriveName { get; set; } = null!;   // "C:"
        public double TotalSizeGb { get; set; }
        public double FreeSizeGb { get; set; }
    }

    // Агент вызывает этот метод (ранее назывался SendUltraMetrics)
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
        Console.WriteLine($"[DATA] {machineName} | CPU: {cpu:F0}% | Drives JSON size {drivesJson.Length}");

        // 1. Находим или создаём запись о машине
        var machine = await _db.Machines
            .FirstOrDefaultAsync(m => m.Name == machineName);

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
        }
        else
        {
            machine.CpuName = cpuName;
            machine.GpuName = gpuName;
            machine.OsVersion = osVersion;
            machine.Status = "Online";
            machine.LastConnected = DateTime.UtcNow;
        }

        // 2. Сохраняем метрику
        var metric = new Metric
        {
            Machine = machine,
            CpuUsage = (float)cpu,
            RamFree = (float)ramFree,
            Timestamp = DateTime.UtcNow
        };
        _db.Metrics.Add(metric);

        // 3. Обновляем информацию по дискам из JSON
        // Ожидаемый формат drivesJson: 
        // [ { "DriveName":"C:", "TotalSizeGb":476, "FreeSizeGb":147 }, ... ]
        List<DriveInfoDto>? drives = null;
        try
        {
            drives = JsonSerializer.Deserialize<List<DriveInfoDto>>(drivesJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Cannot parse drivesJson: {ex.Message}");
        }

        if (drives != null)
        {
            foreach (var d in drives)
            {
                if (string.IsNullOrWhiteSpace(d.DriveName))
                    continue;

                var disk = await _db.Disks
                    .FirstOrDefaultAsync(x =>
                        x.MachineId == machine.Id && x.DriveName == d.DriveName);

                if (disk == null)
                {
                    disk = new Disk
                    {
                        Machine = machine,
                        DriveName = d.DriveName,
                        TotalSizeGb = d.TotalSizeGb,
                        FreeSizeGb = d.FreeSizeGb
                    };
                    _db.Disks.Add(disk);
                }
                else
                {
                    disk.TotalSizeGb = d.TotalSizeGb;
                    disk.FreeSizeGb = d.FreeSizeGb;
                }
            }
        }

        await _db.SaveChangesAsync();

        // 4. По‑прежнему рассылаем данные на дашборд, как было раньше
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
