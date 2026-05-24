using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using NetSentry.Dashboard.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Threading;

namespace NetSentry.Dashboard
{
    public partial class MainWindow : Window
    {
        private HubConnection? _connection;
        private readonly string _authToken;
        private readonly NetworkDevicesService _networkDevicesService;
        private readonly DispatcherTimer _networkRefreshTimer;

        public ObservableCollection<MachineInfo> Machines { get; set; } = new ObservableCollection<MachineInfo>();

        public MainWindow(string token)
        {
            InitializeComponent();
            _authToken = token;
            MachinesList.ItemsSource = Machines;

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            string serverUrl = config["ServerUrl"] ?? "http://localhost:5000/rmmHub";
            string apiBase = config["ApiUrl"] ?? serverUrl.Replace("/rmmHub", "", StringComparison.OrdinalIgnoreCase).TrimEnd('/');

            _networkDevicesService = new NetworkDevicesService(apiBase, _authToken);

            _networkRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _networkRefreshTimer.Tick += async (_, _) => await RefreshAllNetworkDevicesAsync();
            _networkRefreshTimer.Start();

            InitializeSignalR();
        }

        private async void InitializeSignalR()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            string serverUrl = config["ServerUrl"] ?? "http://localhost:5000/rmmHub";

            _connection = new HubConnectionBuilder()
                .WithUrl(serverUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_authToken);
                })
                .WithAutomaticReconnect()
                .Build();

            _connection.On<MetricsPayload>("ReceiveUltraMetrics", (data) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var machine = Machines.FirstOrDefault(m => m.Name == data.MachineName);
                    if (machine == null)
                    {
                        machine = new MachineInfo { Name = data.MachineName };
                        Machines.Add(machine);
                    }

                    machine.UserName = data.UserName;
                    machine.OS = data.OsVersion;
                    machine.Cpu = data.Cpu;
                    machine.RamFree = data.RamFree;
                    machine.CpuName = data.CpuName;
                    machine.GpuName = data.GpuName;
                    machine.Status = "Online";
                    machine.CpuTemp = data.CpuTemp;
                    machine.GpuTemp = data.GpuTemp;

                    try
                    {
                        var disks = JsonSerializer.Deserialize<List<DiskInfo>>(data.DrivesJson);
                        if (disks != null)
                        {
                            machine.Drives.Clear();
                            foreach (var disk in disks)
                                machine.Drives.Add(disk);
                        }
                    }
                    catch
                    {
                    }

                    _ = RefreshNetworkDevicesAsync(machine);
                });
            });

            _connection.On<string>("MachineConnected", (machineName) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var machine = Machines.FirstOrDefault(m => m.Name == machineName);
                    if (machine != null)
                        machine.Status = "Online";
                });
            });

            _connection.On<string>("MachineReconnected", (machineName) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var machine = Machines.FirstOrDefault(m => m.Name == machineName);
                    if (machine != null)
                        machine.Status = "Online";
                });
            });

            _connection.On<string>("MachineDisconnected", (machineName) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var machine = Machines.FirstOrDefault(m => m.Name == machineName);
                    if (machine != null)
                        machine.Status = "Offline";
                });
            });

            try
            {
                await _connection.StartAsync();
                Title = "NetSentry // Big brother is watching you";
                await RefreshAllNetworkDevicesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
        }

        private async Task RefreshAllNetworkDevicesAsync()
        {
            foreach (var machine in Machines.ToList())
            {
                if (machine.Status == "Online")
                    await RefreshNetworkDevicesAsync(machine);
            }
        }

        private async Task RefreshNetworkDevicesAsync(MachineInfo machine)
        {
            try
            {
                var devices = await _networkDevicesService.FetchDevicesAsync(machine.Name);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    machine.NetworkDevices.Clear();
                    foreach (var device in devices)
                        machine.NetworkDevices.Add(device);
                });
            }
            catch
            {
                // сеть недоступна или эндпоинт не отвечает — не ломаем UI
            }
        }

        public class MetricsPayload
        {
            [JsonPropertyName("machineName")]
            public string MachineName { get; set; } = "";

            [JsonPropertyName("userName")]
            public string UserName { get; set; } = "";

            [JsonPropertyName("osVersion")]
            public string OsVersion { get; set; } = "";

            [JsonPropertyName("cpu")]
            public double Cpu { get; set; }

            [JsonPropertyName("ramFree")]
            public double RamFree { get; set; }

            [JsonPropertyName("drivesJson")]
            public string DrivesJson { get; set; } = "";

            [JsonPropertyName("cpuName")]
            public string CpuName { get; set; } = "";

            [JsonPropertyName("gpuName")]
            public string GpuName { get; set; } = "";

            [JsonPropertyName("cpuTemp")]
            public double CpuTemp { get; set; }

            [JsonPropertyName("gpuTemp")]
            public double GpuTemp { get; set; }
        }
    }
}
