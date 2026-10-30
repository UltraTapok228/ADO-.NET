using Dapper;
using Microsoft.Extensions.Configuration;
using TaskTracker.Data;
using TaskTracker.Repositories;

// 1. Загрузка конфигурации
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// 2. Глобальная настройка маппинга snake_case
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// 3. Фабрика соединений
var factory = new PostgresConnectionFactory(
    config.GetConnectionString("Postgres")!);

var users = new UserRepository(factory);
var projects = new ProjectRepository(factory);
var tasks = new TaskRepository(factory);

// 4. Проверка подключения
try
{
    using var conn = factory.Create();
    await conn.QueryAsync("SELECT 1");
    Console.WriteLine("Подключение к PostgreSQL успешно.");
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка подключения: {ex.Message}");
    return;
}

// 5. Меню
while (true)
{
    Console.WriteLine("""
        -------------------------
        1. Создать пользователя
        2. Показать всех пользователей
        3. Создать проект
        4. Найти проект по тегу
        5. Показать проект с задачами
        6. Создать задачу
        7. Массово создать задачи
        8. Отметить задачу выполненной
        9. Показать страницу проектов (пагинация)
        0. Выход
        -------------------------
        """);

    Console.Write("> ");
    var choice = Console.ReadLine();

    try
    {
        switch (choice)
        {
            case "1":
                Console.Write("Имя пользователя: ");
                var userName = Console.ReadLine()!;
                Console.Write("Email: ");
                var email = Console.ReadLine()!;
                var created = await users.CreateAsync(userName, email);
                Console.WriteLine($"Создан пользователь {created.Id}: {created.UserName}");
                break;

            case "2":
                var allUsers = await users.GetAllAsync();
                foreach (var u in allUsers)
                    Console.WriteLine($"{u.Id}. {u.UserName} <{u.Email}>");
                break;

            case "3":
                Console.Write("Название проекта: ");
                var projectName = Console.ReadLine()!;
                Console.Write("ID владельца: ");
                var ownerId = int.Parse(Console.ReadLine()!);
                Console.Write("Теги через запятую: ");
                var tags = Console.ReadLine()!.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var project = await projects.CreateAsync(
                    projectName, ownerId, tags, "{\"theme\":\"dark\"}");
                Console.WriteLine($"Создан проект {project.Id}: {project.Name} [{string.Join(", ", project.Tags)}]");
                break;

            case "4":
                Console.Write("Тег: ");
                var tag = Console.ReadLine()!;
                var found = await projects.GetByTagAsync(tag);
                foreach (var p in found)
                    Console.WriteLine($"{p.Id}. {p.Name}");
                break;

            case "5":
                Console.Write("ID проекта: ");
                var pid = int.Parse(Console.ReadLine()!);
                var withTasks = await projects.GetWithTasksAsync(pid);
                if (withTasks is null)
                {
                    Console.WriteLine("Проект не найден.");
                }
                else
                {
                    Console.WriteLine($"Проект: {withTasks.Name}");
                    foreach (var t in withTasks.Tasks)
                        Console.WriteLine($"  {(t.IsDone ? "✓" : "·")} {t.Id}. {t.Title}");
                }
                break;

            case "6":
                Console.Write("ID проекта: ");
                var pid2 = int.Parse(Console.ReadLine()!);
                Console.Write("Заголовок: ");
                var title = Console.ReadLine()!;
                var task = await tasks.CreateAsync(pid2, title, null, "{\"priority\":\"normal\"}");
                Console.WriteLine($"Создана задача {task.Id}");
                break;

            case "7":
                Console.Write("ID проекта: ");
                var pid3 = int.Parse(Console.ReadLine()!);
                Console.Write("Заголовки через запятую: ");
                var titles = Console.ReadLine()!.Split(',', StringSplitOptions.RemoveEmptyEntries);
                await tasks.BulkInsertAsync(pid3, titles);
                Console.WriteLine($"Вставлено {titles.Length} задач.");
                break;

            case "8":
                Console.Write("ID задачи: ");
                var tid = int.Parse(Console.ReadLine()!);
                var done = await tasks.MarkDoneAsync(tid);
                Console.WriteLine(done is null
                    ? "Задача не найдена или уже выполнена."
                    : $"Задача {done.Id} отмечена выполненной.");
                break;

            case "9":
                Console.Write("Страница: ");
                var page = int.Parse(Console.ReadLine()!);
                var (items, total) = await projects.GetPageAsync(page, 5);
                Console.WriteLine($"Всего: {total}");
                foreach (var p in items)
                    Console.WriteLine($"{p.Id}. {p.Name}");
                break;

            case "0":
                return;

            default:
                Console.WriteLine("Неизвестная команда.");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка: {ex.Message}");
    }

    Console.WriteLine();
}