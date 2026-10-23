using ShopApp.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string not found");

builder.Services.AddSingleton(new DbHelper(connectionString));

var app = builder.Build();

// Инициализация БД с повторными попытками (страховка от race condition)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DbHelper>();
    var retries = 10;
    while (retries > 0)
    {
        try
        {
            db.Initialize();
            Console.WriteLine("База данных инициализирована.");
            break;
        }
        catch (Exception ex)
        {
            retries--;
            Console.WriteLine($"Ожидание PostgreSQL... Осталось попыток: {retries}. {ex.Message}");
            Thread.Sleep(3000);
        }
    }
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Products}/{action=Index}/{id?}");

app.Run();