using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NetSentry.Server.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using NetSentry.Server; // Для RmmHub

var builder = WebApplication.CreateBuilder(args);

// База данных PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Настройка JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                builder.Configuration.GetSection("Jwt:Key").Value!)),
            ValidateIssuer = false,
            ValidateAudience = false
        };

        // Для SignalR: токен передается в query string "access_token"
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/rmmHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Добавляем контроллеры (для API)
builder.Services.AddControllers();

// Настройка сервисов SignalR
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

app.UseAuthentication(); // 1. Кто ты?
app.UseAuthorization();  // 2. Можно ли тебе сюда?

app.MapControllers(); // Подключаем контроллеры (AuthController и другие)

// Регистрируем наш хаб по адресу /rmmHub
app.MapHub<RmmHub>("/rmmHub");

// Заставляем чекать внешний IP 
app.Urls.Add("http://0.0.0.0:5000");

Console.WriteLine("Сервер запущен! Ожидание подключений на порту 5000...");
app.Run();