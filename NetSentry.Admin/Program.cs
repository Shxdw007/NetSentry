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
            Console.WriteLine("\n");
            Console.WriteLine("📊 NetSentry Admin Panel");
            Console.WriteLine("\n[1] ➕ Добавить нового пользователя");
            Console.WriteLine("[2] 📋 Список всех пользователей");
            Console.WriteLine("[3] 🔑 Изменить роль пользователя");
            Console.WriteLine("[4] 🗑️ Удалить пользователя");
            Console.WriteLine("[5] 🔄 Изменить пароль пользователя");
            Console.WriteLine("[6] 🧪 Добавить тестовых пользователей");
            Console.WriteLine("[7] 🚪 Выход");
            Console.Write("\n👉 Выбери опцию [1-7]: ");

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
                    await AddTestUsers();
                    break;
                case "7":
                    Console.WriteLine("\n✅ ББ");
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
        Console.WriteLine("➕ Добавить нового пользователя");
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

        string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

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
        Console.WriteLine("📋 Список пользователей\n");

        // Параметры пагинации и фильтрации
        int pageSize = 10; // Сколько записей показывать на странице
        int currentPage = 1;
        string searchQuery = "";
        string roleFilter = "Все"; // "Все", "Admin", "Viewer"
        string sortOrder = "ID"; // "ID", "Логин", "Роль"

        while (true)
        {
            Console.Clear();
            Console.WriteLine("📋 Список пользователей\n");

            // Показываем текущие фильтры
            Console.WriteLine($"🔍 Поиск: {(string.IsNullOrEmpty(searchQuery) ? "не задан" : $"'{searchQuery}'")}");
            Console.WriteLine($"🎯 Фильтр роли: {roleFilter}");
            Console.WriteLine($"⬆️ Сортировка: {sortOrder}\n");

            // Строим запрос
            IQueryable<User> query = _context.Users;

            // Применяем поиск
            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = query.Where(u => u.Username.Contains(searchQuery));
            }

            // Применяем фильтр по роли
            if (roleFilter != "Все")
            {
                query = query.Where(u => u.Role == roleFilter);
            }

            // Применяем сортировку
            query = sortOrder switch
            {
                "ID" => query.OrderBy(u => u.Id),
                "Логин" => query.OrderBy(u => u.Username),
                "Роль" => query.OrderBy(u => u.Role).ThenBy(u => u.Id),
                "Роль DESC" => query.OrderByDescending(u => u.Role).ThenBy(u => u.Id),
                _ => query.OrderBy(u => u.Id)
            };

            // Считаем общее количество
            int totalUsers = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);

            if (totalPages == 0) totalPages = 1;
            if (currentPage > totalPages) currentPage = totalPages;
            if (currentPage < 1) currentPage = 1;

            // Получаем пользователей для текущей страницы
            var users = await query
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (totalUsers == 0)
            {
                Console.WriteLine("❌ Пользователей не найдено!\n");
            }
            else
            {
                Console.WriteLine($"Всего найдено: {totalUsers} | Страница {currentPage} из {totalPages}\n");

                // Таблица с пользователями 
                Console.WriteLine("┌──────┬─────────────────────────┬──────────┐");
                Console.WriteLine("│ ID   │ Логин                   │ Роль     │");
                Console.WriteLine("├──────┼─────────────────────────┼──────────┤");

                foreach (var user in users)
                {
                    string displayLogin = user.Username.Length > 23
                        ? user.Username.Substring(0, 20) + "..."
                        : user.Username;

                    Console.WriteLine($"│ {user.Id,-4} │ {displayLogin,-23} │ {user.Role,-8} │");
                }

                Console.WriteLine("└──────┴─────────────────────────┴──────────┘\n");
            }


            // Меню навигации
            Console.WriteLine("───────────── Навигация ─────────────");
            Console.WriteLine("[N] ➡️  Следующая страница");
            Console.WriteLine("[P] ⬅️  Предыдущая страница");
            Console.WriteLine("[G] 🎯 Перейти на страницу...");
            Console.WriteLine("[S] 🔍 Поиск по логину");
            Console.WriteLine("[F] 🎛️  Фильтр по роли");
            Console.WriteLine("[O] ⬆️  Сортировка");
            Console.WriteLine("[R] 🔄 Сбросить все фильтры");
            Console.WriteLine("[Q] 🚪 Назад в главное меню");
            Console.Write("\n👉 Выбери опцию: ");

            string choice = Console.ReadLine()?.ToLower() ?? "";

            switch (choice)
            {
                case "n": 
                    if (currentPage < totalPages)
                        currentPage++;
                    else
                        Console.WriteLine("\n⚠️ Это последняя страница!");
                    break;

                case "p": 
                    if (currentPage > 1)
                        currentPage--;
                    else
                        Console.WriteLine("\n⚠️ Это первая страница!");
                    break;

                case "g": 
                    Console.Write($"\nВведи номер страницы (1-{totalPages}): ");
                    if (int.TryParse(Console.ReadLine(), out int pageNum) && pageNum >= 1 && pageNum <= totalPages)
                    {
                        currentPage = pageNum;
                    }
                    else
                    {
                        Console.WriteLine("❌ Неверный номер страницы!");
                        System.Threading.Thread.Sleep(1000);
                    }
                    break;

                case "s": 
                    Console.Write("\n🔍 Введи логин для поиска (или ENTER для отмены): ");
                    string search = Console.ReadLine() ?? "";
                    searchQuery = search;
                    currentPage = 1; 
                    break;

                case "f":
                    Console.WriteLine("\n🎛️  Выбери фильтр:");
                    Console.WriteLine("[1] Все пользователи");
                    Console.WriteLine("[2] Только Admin");
                    Console.WriteLine("[3] Только Viewer");
                    Console.Write("👉 Выбор [1-3]: ");
                    string filterChoice = Console.ReadLine() ?? "";
                    roleFilter = filterChoice switch
                    {
                        "1" => "Все",
                        "2" => "Admin",
                        "3" => "Viewer",
                        _ => roleFilter
                    };
                    currentPage = 1;
                    break;

                case "o": 
                    Console.WriteLine("\n⬆️  Выбери сортировку:");
                    Console.WriteLine("[1] По ID");
                    Console.WriteLine("[2] По логину");
                    Console.WriteLine("[3] Сначала Admin, потом Viewer");
                    Console.WriteLine("[4] Сначала Viewer, потом Admin");
                    Console.Write("👉 Выбор [1-4]: ");
                    string sortChoice = Console.ReadLine() ?? "";
                    sortOrder = sortChoice switch
                    {
                        "1" => "ID",
                        "2" => "Логин",
                        "3" => "Роль",
                        "4" => "Роль DESC ",
                        _ => sortOrder
                    };
                    currentPage = 1;
                    break;

                case "r": 
                    searchQuery = "";
                    roleFilter = "Все";
                    sortOrder = "ID";
                    currentPage = 1;
                    Console.WriteLine("\n✅ Фильтры сброшены!");
                    System.Threading.Thread.Sleep(800);
                    break;

                case "q": // Quit
                    return;

                default:
                    Console.WriteLine("\n❌ Неверный выбор!");
                    System.Threading.Thread.Sleep(800);
                    break;
            }
        }
    }

    static async Task ChangeUserRole()
    {
        Console.Clear();
        Console.WriteLine("🔑 Изменить роль пользователя");
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
        Console.WriteLine("🗑️ Удалить пользователя\n");
        Console.WriteLine("Введи логин пользователя для удаления");
        Console.WriteLine("Или введи 'ALL' для удаления ВСЕХ пользователей");
        Console.Write("\n👉 Логин: ");
        string username = Console.ReadLine();

        // Проверяем, хочет ли пользователь удалить всех
        if (username?.ToUpper() == "ALL")
        {
            await DeleteAllUsers();
            return;
        }

        // Обычное удаление одного пользователя
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            Console.WriteLine($"❌ Пользователь '{username}' не найден!");
            PressAnyKey();
            return;
        }

        Console.Write($"\n⚠️ Ты точно хочешь удалить пользователя '{username}'? [y/n]: ");
        string confirm = Console.ReadLine()?.ToLower();

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

    static async Task DeleteAllUsers()
    {
        Console.Clear();
        Console.WriteLine("🗑️💥 УДАЛЕНИЕ ВСЕХ ПОЛЬЗОВАТЕЛЕЙ\n");

        // Считаем общее количество
        int totalUsers = await _context.Users.CountAsync();

        if (totalUsers == 0)
        {
            Console.WriteLine("❌ В базе нет пользователей!");
            PressAnyKey();
            return;
        }

        Console.WriteLine($"⚠️  В базе данных найдено пользователей: {totalUsers}");
        Console.WriteLine("⚠️  ЭТО ДЕЙСТВИЕ НЕОБРАТИМО!");
        Console.WriteLine("\nВведи 'DELETE ALL' для подтверждения полного удаления");
        Console.Write("👉 Подтверждение: ");

        string confirmation = Console.ReadLine();

        if (confirmation != "DELETE ALL")
        {
            Console.WriteLine("\n❌ Удаление отменено. Данные сохранены.");
            PressAnyKey();
            return;
        }

        // Дополнительное подтверждение
        Console.Write("\n⚠️  Последний шанс! Ты точно уверен? [y/n]: ");
        string finalConfirm = Console.ReadLine()?.ToLower();

        if (finalConfirm != "y")
        {
            Console.WriteLine("\n❌ Удаление отменено");
            PressAnyKey();
            return;
        }

        // Удаляем всех пользователей
        Console.WriteLine("\n🔄 Удаление в процессе...");

        var allUsers = await _context.Users.ToListAsync();
        _context.Users.RemoveRange(allUsers);
        await _context.SaveChangesAsync();

        Console.WriteLine($"\n✅ Успешно удалено {totalUsers} пользователей!");
        Console.WriteLine("База данных полностью очищена.");
        PressAnyKey();
    }


    static async Task ChangePassword()
    {
        Console.Clear();
        Console.WriteLine("🔄 Изменить пароль пользователя");
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

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _context.SaveChangesAsync();

        Console.WriteLine($"\n✅ Пароль пользователя '{username}' изменен");
        PressAnyKey();
    }

    static async Task AddTestUsers()
    {
        Console.Clear();
        Console.WriteLine("🧪 Генерация тестовых пользователей\n");

        Console.Write("Сколько пользователей создать?: ");
        if (!int.TryParse(Console.ReadLine(), out int count) || count <= 0)
        {
            Console.WriteLine("❌ Неверное число!");
            PressAnyKey();
            return;
        }

        Console.Write($"\n⚠️ Будет создано {count} пользователей. Продолжить? [y/n]: ");
        if (Console.ReadLine()?.ToLower() != "y")
        {
            Console.WriteLine("❌ Отменено");
            PressAnyKey();
            return;
        }

        Random random = new Random();
        string[] firstNames = { "Александр", "Дмитрий", "Иван", "Максим", "Артем", "Никита", "Михаил", "Андрей", "Егор", "Сергей" };
        string[] lastNames = { "Иванов", "Петров", "Сидоров", "Козлов", "Новиков", "Морозов", "Волков", "Соколов", "Зайцев", "Лебедев" };

        int created = 0;
        for (int i = 1; i <= count; i++)
        {
            string firstName = firstNames[random.Next(firstNames.Length)];
            string lastName = lastNames[random.Next(lastNames.Length)];
            string username = $"{firstName}_{lastName}_{i}";

            // Проверяем, существует ли уже
            var exists = await _context.Users.AnyAsync(u => u.Username == username);
            if (exists)
            {
                username = $"User_{Guid.NewGuid().ToString().Substring(0, 8)}";
            }

            string role = random.Next(100) < 30 ? "Admin" : "Viewer"; 
            string passwordHash = BCrypt.Net.BCrypt.HashPassword("test123");

            var newUser = new User
            {
                Username = username,
                PasswordHash = passwordHash,
                Role = role
            };

            _context.Users.Add(newUser);
            created++;

            if (i % 10 == 0) 
            {
                await _context.SaveChangesAsync();
                Console.Write($"\r✅ Создано: {created}/{count}");
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"\n\n🎉 Успешно создано {created} пользователей!");
        PressAnyKey();
    }

    static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"

            🛡️ NetSentry Admin Console 🛡️

            Управление пользователями и доступом

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
