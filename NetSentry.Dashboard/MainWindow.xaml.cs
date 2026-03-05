using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

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
            connection.On<string, string, string, double, double, string, string, string>(
                "ReceiveUltraMetrics",
                (name, user, os, cpu, ram, drivesJson, cpuName, gpuName) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var machine = Machines.FirstOrDefault(m => m.Name == name);
                        if (machine == null)
                        {
                            machine = new MachineInfo { Name = name };
                            Machines.Add(machine);
                        }

                        machine.UserName = user;
                        machine.OS = os;
                        machine.Cpu = cpu;
                        machine.RamFree = ram;
                        machine.CpuName = cpuName;
                        machine.GpuName = gpuName;

                        try
                        {
                            var disks = JsonSerializer.Deserialize<List<DiskInfo>>(drivesJson);

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

            //  Обработчик отключения всех машин
            connection.On("AllMachinesOffline", () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Console.WriteLine($"[DASHBOARD] Все машины offline");
                    foreach (var machine in Machines)
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
    }
}
