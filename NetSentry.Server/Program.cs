using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Настройка сервисов 
builder.Services.AddSignalR();
builder.Services.AddCors(options => options.AddPolicy("AllowAll", policy =>
{
    policy.AllowAnyHeader()
          .AllowAnyMethod()
          .SetIsOriginAllowed((host) => true) 
          .AllowCredentials();
}));

var app = builder.Build();

// Настройка пайплайна 
app.UseCors("AllowAll");

// Регистрируем наш хаб по адресу /rmmHub
app.MapHub<RmmHub>("/rmmHub");

// Заставляем чекать внешний IP 
app.Urls.Add("http://0.0.0.0:5000");

Console.WriteLine("🚀 Сервер запущен! Ожидание подключений на порту 5000...");
app.Run();
