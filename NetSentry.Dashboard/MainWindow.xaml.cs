using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Text.Json.Serialization;

namespace NetSentry.Dashboard
{
    public partial class MainWindow : Window
    {
        HubConnection connection;
        private readonly string _authToken; 

        public ObservableCollection<MachineInfo> Machines { get; set; } = new ObservableCollection<MachineInfo>();

        public MainWindow(string token)
        {
            InitializeComponent();
            _authToken = token; 
            MachinesList.ItemsSource = Machines;
            InitializeSignalR();
        }

        private async void InitializeSignalR()
        {
            var config = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: true)
              .Build();

            string serverUrl = config["ServerUrl"] ?? "http://localhost:5000/rmmHub";

            connection = new HubConnectionBuilder()
                .WithUrl(serverUrl, options =>
                {
                    options.AccessTokenProvider = () => System.Threading.Tasks.Task.FromResult(_authToken);
                })
                .WithAutomaticReconnect()
                .Build();

            // Основной обработчик метрик
            connection.On<MetricsPayload>("ReceiveUltraMetrics", (data) =>
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
                                {
                                    machine.Drives.Add(disk);
                                }
                            }
                        }
                        catch
                        {
                        }
                    });
                });

            //  Обработчик новой машины
            connection.On<string>("MachineConnected", (machineName) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Console.WriteLine($"[DASHBOARD] Машина подключилась: {machineName}");
                    var machine = Machines.FirstOrDefault(m => m.Name == machineName);
                    if (machine != null)
                    {
                        machine.Status = "Online";
                        UpdateMachineStatusUI(machine);
                    }
                });
            });

            // Обработчик переподключения
            connection.On<string>("MachineReconnected", (machineName) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Console.WriteLine($"[DASHBOARD] Машина переподключилась: {machineName}");
                    var machine = Machines.FirstOrDefault(m => m.Name == machineName);
                    if (machine != null)
                    {
                        machine.Status = "Online";
                    }
                });
            });

            // Обработчик отключения конкретной машины
            connection.On<string>("MachineDisconnected", (machineName) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Console.WriteLine($"[DASHBOARD] Машина отключилась: {machineName}");
                    var machine = Machines.FirstOrDefault(m => m.Name == machineName);
                    if (machine != null)
                    {
                        machine.Status = "Offline";
                    }
                });
            });

            try
            {
                await connection.StartAsync();
                Title = "NetSentry // Big brother is watching you";
                Console.WriteLine("[DASHBOARD] Подключено к серверу");
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
        }
        private void UpdateMachineStatusUI(MachineInfo machine)
        {
            if (machine.Status == "Online")
            {
                // Зелёный
                // StatusBorder.Background = новый цвет
                // StatusBorder.BorderBrush = #00FF41
            }
            else
            {
                // Красный
                // StatusBorder.Background = новый цвет
                // StatusBorder.BorderBrush = #FF0000
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
