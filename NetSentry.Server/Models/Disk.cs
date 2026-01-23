namespace NetSentry.Server.Models;

public class Disk
{
    public int Id { get; set; }

    public int MachineId { get; set; }
    public Machine Machine { get; set; } = null!;   // FK на Machines

    public string DriveName { get; set; } = null!;  // буква диска, C:, D:
    public double TotalSizeGb { get; set; }         // total_size_gb
    public double FreeSizeGb { get; set; }          // free_size_gb

    public double UsagePercent =>
        TotalSizeGb <= 0 ? 0 : (TotalSizeGb - FreeSizeGb) / TotalSizeGb * 100.0;
}
