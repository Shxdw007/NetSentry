using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace NetSentry.Dashboard
{
    public class MachineInfo : INotifyPropertyChanged
    {
        private double _cpu;
        private double _ramFree;
        private string _status = "Online";

        // --- НОВЫЕ ПЕРЕМЕННЫЕ ДЛЯ ТЕМПЕРАТУРЫ ---
        private double _cpuTemp;
        private double _gpuTemp;

        // Основная инфо
        public required string Name { get; set; }
        public string UserName { get; set; } = "Unknown";
        public string OS { get; set; } = "Windows";

        // Железо 
        public string CpuName { get; set; } = "Scanning...";
        public string GpuName { get; set; } = "Scanning...";

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string StatusDisplay => Status == "Online"
           ? "🟢 Online"
           : "🔴 Offline";

        public ObservableCollection<DiskInfo> Drives { get; set; } = new ObservableCollection<DiskInfo>();

        public ObservableCollection<NetworkDeviceDto> NetworkDevices { get; set; } =
            new ObservableCollection<NetworkDeviceDto>();

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

        // --- СВОЙСТВА ДЛЯ ТЕМПЕРАТУРЫ---
        public double CpuTemp
        {
            get => _cpuTemp;
            set { _cpuTemp = value; OnPropertyChanged(); OnPropertyChanged(nameof(CpuTempColor)); }
        }

        public double GpuTemp
        {
            get => _gpuTemp;
            set { _gpuTemp = value; OnPropertyChanged(); OnPropertyChanged(nameof(GpuTempColor)); }
        }

        // Логика цветов (если >= 80, то красный, иначе фирменный зеленый)
        public string CpuTempColor => CpuTemp >= 80 ? "#FF003C" : "#00FF41";
        public string GpuTempColor => GpuTemp >= 80 ? "#FF003C" : "#00FF41";

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
        public string DriveName { get; set; }      // ← НОВОЕ: DriveName
        public double TotalSizeGb { get; set; }    // ← НОВОЕ: TotalSizeGb
        public double FreeSizeGb { get; set; }     // ← НОВОЕ: FreeSizeGb

        public double UsagePercent => TotalSizeGb > 0 ? (1.0 - (FreeSizeGb / TotalSizeGb)) * 100 : 0;
        public string DisplayText => $"{DriveName} {FreeSizeGb:F0}GB free / {TotalSizeGb:F0}GB";
    }
}