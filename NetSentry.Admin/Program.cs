using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetSentry.Server.Data;  
using NetSentry.Server.Models;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class Program
{
    private static AppDbContext _context;

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(config.GetConnectionString("DefaultConnection"));

        _context = new AppDbContext(optionsBuilder.Options);

        try
        {
            var test = await _context.Database.CanConnectAsync();
            if (!test)
            {
                Console.WriteLine("❌ Ошибка: Не удалось подключиться к БД!");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка подключения: {ex.Message}");
            return;
        }

        Console.Clear();
        PrintBanner();

        while (true)
        {
            Console.WriteLine("\n╔════════════════════════════════════════╗");
            Console.WriteLine("║   📊 NetSentry Admin Panel              ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine("\n[1] ➕ Добавить нового пользователя");
            Console.WriteLine("[2] 📋 Список всех пользователей");
            Console.WriteLine("[3] 🔑 Изменить роль пользователя");
            Console.WriteLine("[4] 🗑️  Удалить пользователя");
            Console.WriteLine("[5] 🔄 Изменить пароль пользователя");
            Console.WriteLine("[6] 🚪 Выход");
            Console.Write("\n👉 Выбери опцию [1-6]: ");

            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await AddUser();
                    break;
                case "2":
                    await ListUsers();
                    break;
                case "3":
                    await ChangeUserRole();
                    break;
                case "4":
                    await DeleteUser();
                    break;
                case "5":
                    await ChangePassword();
                    break;
                case "6":
                    Console.WriteLine("\n✅ До встречи!");
                    return;
                default:
                    Console.WriteLine("\n❌ Неверный выбор!");
                    break;
            }
        }
    }

    static async Task AddUser()
    {
        Console.Clear();
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║   ➕ Добавить нового пользователя       ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        Console.Write("Введи логин пользователя: ");
        string username = Console.ReadLine();

        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (existingUser != null)
        {
            Console.WriteLine($"❌ Пользователь '{username}' уже существует!");
            PressAnyKey();
            return;
        }

        Console.Write("Введи пароль: ");
        string password = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(password) || password.Length < 5)
        {
            Console.WriteLine("❌ Пароль должен быть минимум 5 символов!");
            PressAnyKey();
            return;
        }

        Console.WriteLine("\nВыбери роль:");
        Console.WriteLine("[1] Admin (полный доступ)");
        Console.WriteLine("[2] Viewer (только просмотр)");
        Console.Write("👉 Выбор [1-2]: ");
        string roleChoice = Console.ReadLine();

        string role = roleChoice == "1" ? "Admin" : "Viewer";

        string passwordHash = BCrypt.Net.BCrypt.HashPassword(password); // ← Правильный путь!

        var newUser = new User
        {
            Username = username,
            PasswordHash = passwordHash,
            Role = role
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        Console.WriteLine($"\n✅ Пользователь '{username}' добавлен с ролью '{role}'");
        PressAnyKey();
    }

    static async Task ListUsers()
    {
        Console.Clear();
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║   📋 Список пользователей              ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        var users = await _context.Users.ToListAsync();

        if (users.Count == 0)
        {
            Console.WriteLine("❌ Пользователей нет!");
            PressAnyKey();
            return;
        }

        Console.WriteLine($"Всего пользователей: {users.Count}\n");
        Console.WriteLine("┌─────┬──────────────────┬──────────┐");
        Console.WriteLine("│ ID  │ Логин            │ Роль     │");
        Console.WriteLine("├─────┼──────────────────┼──────────┤");

        foreach (var user in users)
        {
            Console.WriteLine($"│ {user.Id,-3} │ {user.Username,-16} │ {user.Role,-8} │");
        }

        Console.WriteLine("└─────┴──────────────────┴──────────┘");
        PressAnyKey();
    }

    static async Task ChangeUserRole()
    {
        Console.Clear();
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║   🔑 Изменить роль пользователя        ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        Console.Write("Введи логин пользователя: ");
        string username = Console.ReadLine();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            Console.WriteLine($"❌ Пользователь '{username}' не найден!");
            PressAnyKey();
            return;
        }

        Console.WriteLine($"Текущая роль: {user.Role}");
        Console.WriteLine("\nНовая роль:");
        Console.WriteLine("[1] Admin");
        Console.WriteLine("[2] Viewer");
        Console.Write("👉 Выбор [1-2]: ");
        string roleChoice = Console.ReadLine();

        string newRole = roleChoice == "1" ? "Admin" : "Viewer";

        user.Role = newRole;
        await _context.SaveChangesAsync();

        Console.WriteLine($"\n✅ Роль пользователя '{username}' изменена на '{newRole}'");
        PressAnyKey();
    }

    static async Task DeleteUser()
    {
        Console.Clear();
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║   🗑️  Удалить пользователя             ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        Console.Write("Введи логин пользователя: ");
        string username = Console.ReadLine();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            Console.WriteLine($"❌ Пользователь '{username}' не найден!");
            PressAnyKey();
            return;
        }

        Console.WriteLine($"\n⚠️  Ты точно хочешь удалить пользователя '{username}'? [y/n]: ");
        string confirm = Console.ReadLine().ToLower();

        if (confirm != "y")
        {
            Console.WriteLine("❌ Удаление отменено");
            PressAnyKey();
            return;
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        Console.WriteLine($"\n✅ Пользователь '{username}' удален");
        PressAnyKey();
    }

    static async Task ChangePassword()
    {
        Console.Clear();
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║   🔄 Изменить пароль пользователя      ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        Console.Write("Введи логин пользователя: ");
        string username = Console.ReadLine();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            Console.WriteLine($"❌ Пользователь '{username}' не найден!");
            PressAnyKey();
            return;
        }

        Console.Write("Введи новый пароль: ");
        string newPassword = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 5)
        {
            Console.WriteLine("❌ Пароль должен быть минимум 5 символов!");
            PressAnyKey();
            return;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword); // ← Правильный путь!
        await _context.SaveChangesAsync();

        Console.WriteLine($"\n✅ Пароль пользователя '{username}' изменен");
        PressAnyKey();
    }

    static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔══════════════════════════════════════════════════╗
║          🛡️  NetSentry Admin Console 🛡️           ║
║                                                  ║
║     Управление пользователями и доступом        ║
╚══════════════════════════════════════════════════╝
        ");
        Console.ResetColor();
    }

    static void PressAnyKey()
    {
        Console.WriteLine("\nНажми ENTER для продолжения...");
        Console.ReadLine();
        Console.Clear();
    }
}
