using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetSentry.Dashboard
{
    public class MachineInfo : INotifyPropertyChanged
    {
        private double _cpu;
        private double _ramFree;
        private double _diskFree;
        private double _diskTotal;

        
        public required string Name { get; set; }
        public string UserName { get; set; } = "Unknown";
        public string OS { get; set; } = "Windows";

        public double Cpu
        {
            get => _cpu;
            set { _cpu = value; OnPropertyChanged(); OnPropertyChanged(nameof(CpuDisplay)); }
        }

        public double RamFree
        {
            get => _ramFree;
            set { _ramFree = value; OnPropertyChanged(); OnPropertyChanged(nameof(RamDisplay)); }
        }

        public double DiskFree
        {
            get => _diskFree;
            set { _diskFree = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiskDisplay)); OnPropertyChanged(nameof(DiskUsagePercent)); }
        }

        public double DiskTotal
        {
            get => _diskTotal;
            set { _diskTotal = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiskUsagePercent)); }
        }

        
        public string CpuDisplay => $"{Cpu:F0}%";
        public string RamDisplay => $"{RamFree:F0} MB";
        public string DiskDisplay => $"{DiskFree:F0} GB";

        public double DiskUsagePercent => DiskTotal > 0 ? (1.0 - (DiskFree / DiskTotal)) * 100 : 0;

        public event PropertyChangedEventHandler? PropertyChanged; // Добавь '?'
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
