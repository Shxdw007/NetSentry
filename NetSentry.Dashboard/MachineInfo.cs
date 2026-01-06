using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel; 

namespace NetSentry.Dashboard
{
    public class MachineInfo : INotifyPropertyChanged
    {
        private double _cpu;
        private double _ramFree;

        // Основная инфо
        public required string Name { get; set; }
        public string UserName { get; set; } = "Unknown";
        public string OS { get; set; } = "Windows";

        // Железо 
        public string CpuName { get; set; } = "Scanning...";
        public string GpuName { get; set; } = "Scanning...";

        
        public ObservableCollection<DiskInfo> Drives { get; set; } = new ObservableCollection<DiskInfo>();

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

        public string CpuDisplay => $"{Cpu:F0}%";
        public string RamDisplay => $"{RamFree:F0} MB";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class DiskInfo
    {
        public string Name { get; set; } // "C:\"
        public double Total { get; set; }
        public double Free { get; set; }

        public double UsagePercent => Total > 0 ? (1.0 - (Free / Total)) * 100 : 0;
        public string DisplayText => $"{Name} {Free:F0}GB free / {Total:F0}GB";
    }
}
