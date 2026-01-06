using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using System.Windows;
using System.Linq;
using System.Text.Json; 
using System.Collections.Generic;

namespace NetSentry.Dashboard
{
    public partial class MainWindow : Window
    {
        HubConnection connection;

        public ObservableCollection<MachineInfo> Machines { get; set; } = new ObservableCollection<MachineInfo>();

        public MainWindow()
        {
            InitializeComponent();
            MachinesList.ItemsSource = Machines; 
            InitializeSignalR();
        }

        private async void InitializeSignalR()
        {
            string serverUrl = "http://192.168.3.61:5000/rmmHub";

            connection = new HubConnectionBuilder()
                .WithUrl(serverUrl)
                .WithAutomaticReconnect()
                .Build();

            
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

            try
            {
                await connection.StartAsync();
                Title = "NetSentry // Big brother is watching you";
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
        }
    }
}
