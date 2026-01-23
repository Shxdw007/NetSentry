using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NetSentry.Server.Data;


var builder = WebApplication.CreateBuilder(args);

// База данных PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

Console.WriteLine("Сервер запущен! Ожидание подключений на порту 5000...");
app.Run();
