namespace NetSentry.Server.Models;

public class Machine
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;       // name
    public string CpuName { get; set; } = null!;    // cpu_name
    public string GpuName { get; set; } = null!;    // gpu_name
    public string OsVersion { get; set; } = null!;  // os_version
    public string Status { get; set; } = null!;     // status (Online/Offline)

    public DateTime FirstConnected { get; set; }    // first_connected
    public DateTime LastConnected { get; set; }     // last_connected

    public ICollection<Metric> Metrics { get; set; } = new List<Metric>();
    public ICollection<Disk> Disks { get; set; } = new List<Disk>();
}
