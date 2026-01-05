using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using System.Windows;
using System.Linq;

namespace NetSentry.Dashboard
{
    public partial class MainWindow : Window
    {
        HubConnection connection;
        // Коллекция, которая автоматически обновляет список на экране
        public ObservableCollection<MachineInfo> Machines { get; set; } = new ObservableCollection<MachineInfo>();

        public MainWindow()
        {
            InitializeComponent();

            // Привязываем список к интерфейсу
            MachinesList.ItemsSource = Machines;

            InitializeSignalR();
        }

        private async void InitializeSignalR()
        {
            // 1. Указываем адрес 
            string serverUrl = "http://192.***.*.**:5000/rmmHub";

            connection = new HubConnectionBuilder()
                .WithUrl(serverUrl)
                .WithAutomaticReconnect()
                .Build();

            // 2."ReceiveFullMetrics"
            connection.On<string, string, string, double, double, double, double>(
                "ReceiveFullMetrics",
                (name, user, os, cpu, ram, diskTotal, diskFree) =>
                {
                    // Обновляем UI только через Dispatcher
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Ищем комп в списке
                        var machine = Machines.FirstOrDefault(m => m.Name == name);

                        // Если компа нет — создаем новый
                        if (machine == null)
                        {
                            machine = new MachineInfo { Name = name };
                            Machines.Add(machine);
                        }

                        // Заполняем ВСЕ данные
                        machine.UserName = user;
                        machine.OS = os;
                        machine.Cpu = cpu;
                        machine.RamFree = ram;
                        machine.DiskTotal = diskTotal;
                        machine.DiskFree = diskFree;
                    });
                });

            // 3. Запускаем соединение
            try
            {
                await connection.StartAsync();
                Title = "NetSentry // Big brother is watching you";
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к серверу: {ex.Message}");
            }
        }

    }
}
