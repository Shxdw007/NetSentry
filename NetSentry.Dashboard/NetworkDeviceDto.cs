using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetSentry.Dashboard;

public class NetworkDeviceDto : INotifyPropertyChanged
{
    private string _ipAddress = "";
    private string _macAddress = "";
    private DateTime _lastSeenAtUtc;

    public string IpAddress
    {
        get => _ipAddress;
        set { _ipAddress = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayLine)); }
    }

    public string MacAddress
    {
        get => _macAddress;
        set { _macAddress = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayLine)); }
    }

    public DateTime LastSeenAtUtc
    {
        get => _lastSeenAtUtc;
        set { _lastSeenAtUtc = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayLine)); OnPropertyChanged(nameof(IsActive)); }
    }

    public bool IsActive => (DateTime.UtcNow - LastSeenAtUtc).TotalMinutes < 30;

    public string DisplayLine => $"{IpAddress} | {MacAddress} | Активен";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
