namespace NetSentry.Server.Models;

public class Metric
{
    public int Id { get; set; }

    public int MachineId { get; set; }
    public Machine Machine { get; set; } = null!;   // FK на Machines

    public float CpuUsage { get; set; }             // cpu_usage (0–100)
    public float RamFree { get; set; }              // ram_free в МБ

    // --- НОВЫЕ ПОЛЯ ДЛЯ ТЕМПЕРАТУРЫ ---
    public float CpuTemp { get; set; }
    public float GpuTemp { get; set; }

    public DateTime Timestamp { get; set; }         // timestamp
}