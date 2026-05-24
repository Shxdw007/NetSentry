using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Configuration;

namespace NetSentry.Dashboard
{
    public partial class LoginWindow : Window
    {
        private readonly string _apiBaseUrl;

        public LoginWindow()
        {
            InitializeComponent();

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            string serverUrl = config["ServerUrl"] ?? "http://localhost:80/rmmHub";
            _apiBaseUrl = (config["ApiUrl"] ?? serverUrl.Replace("/rmmHub", "", StringComparison.OrdinalIgnoreCase))
                .TrimEnd('/');
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var loginData = new { username = UsernameBox.Text.Trim(), password = PasswordBox.Password };

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var json = JsonSerializer.Serialize(loginData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"{_apiBaseUrl}/api/auth/login", content);

                var responseBody = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                if (response.IsSuccessStatusCode)
                {
                    var wrapper = JsonSerializer.Deserialize<ApiResponse<AuthData>>(responseBody, options);

                    if (wrapper?.Success == true && !string.IsNullOrWhiteSpace(wrapper.Data?.Token))
                    {
                        var mainWindow = new MainWindow(wrapper.Data.Token);
                        mainWindow.Show();
                        Close();
                        return;
                    }

                    ErrorText.Text = wrapper?.Message ?? "Ошибка разбора ответа сервера";
                    return;
                }

                ErrorText.Text = TryReadErrorMessage(responseBody, options)
                    ?? $"Ошибка входа ({(int)response.StatusCode})";
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Нет связи с сервером: " + ex.Message;
            }
        }

        private static string? TryReadErrorMessage(string json, JsonSerializerOptions options)
        {
            try
            {
                var wrapper = JsonSerializer.Deserialize<ApiResponse<object>>(json, options);
                if (!string.IsNullOrWhiteSpace(wrapper?.Message))
                    return wrapper.Message;
            }
            catch
            {
                // ignore
            }

            return null;
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

    public sealed class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public T? Data { get; set; }
    }

    public sealed class AuthData
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = "";

        [JsonPropertyName("role")]
        public string Role { get; set; } = "";
    }
}
