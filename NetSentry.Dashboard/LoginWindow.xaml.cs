using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace NetSentry.Dashboard
{
    public partial class LoginWindow : Window
    {
        private readonly string _serverUrl;

        public LoginWindow()
        {
            InitializeComponent();
            
            // Загружаем конфиг, чтобы знать куда стучаться
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            var config = builder.Build();
            _serverUrl = config["ServerUrl"];
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var loginData = new
            {
                username = UsernameBox.Text,
                password = PasswordBox.Password
            };

            try
            {
                using var client = new HttpClient();
                var json = JsonSerializer.Serialize(loginData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("http://localhost:5000/api/auth/login", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var authResult = JsonSerializer.Deserialize<AuthResponse>(responseBody, options);

                    // Открываем главное окно и передаем токен
                    var mainWindow = new MainWindow(authResult.Token);
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    ErrorText.Text = "Invalid username or password";
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Connection error: " + ex.Message;
            }
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }

    public class AuthResponse
    {
        public string Token { get; set; }
        public string Role { get; set; }
    }
}