using Microsoft.AspNetCore.Mvc;
using AutoPartsApp.Data;
using AutoPartsApp.Models;

namespace AutoPartsApp.Controllers;

public class PartsController : Controller
{
    private readonly DbHelper _db;

    public PartsController(DbHelper db)
    {
        _db = db;
    }

    // GET /Parts
    public async Task<IActionResult> Index()
    {
        var parts = await _db.GetAllPartsAsync();
        return View(parts);
    }

    // GET /Parts/Details/5
    public IActionResult Details(int id)
    {
        var part = _db.GetPartById(id);
        if (part == null) return NotFound();
        return View(part);
    }

    // GET /Parts/Create - этот метод ОТКРЫВАЕТ форму
    public IActionResult Create()
    {
        return View();
    }

    // POST /Parts/Create - этот метод СОХРАНЯЕТ данные из формы
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Part part)
    {
        if (ModelState.IsValid)
        {
            var result = _db.TryAddPart(part);
            if (result.Ok) return RedirectToAction(nameof(Index));
            
            ModelState.AddModelError("Article", result.Error!);
        }
        return View(part);
    }

    // POST /Parts/Buy/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Buy(int id, int quantity = 1)
    {
        var success = _db.PlaceOrder(id, quantity);
        TempData["Message"] = success
            ? $"Заказ успешно оформлен! Куплено {quantity} шт."
            : "Ошибка оформления заказа (возможно, недостаточно товара).";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Search(string query)
    {
        var parts = _db.SearchParts(query);
        ViewData["SearchQuery"] = query; // чтобы текст остался в строке поиска
        return View("Index", parts);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var success = _db.DeletePart(id);
        TempData["Message"] = success ? "Запчасть успешно удалена!" : "Запчасть не найдена.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Stats()
    {
        var stats = _db.GetStats();
        return View(stats);
    }

    public IActionResult Restock() => View();

    [HttpPost]
    public IActionResult Restock(string itemsText)
    {
        // Парсим текст вида "1:50, 2:10" (Id:Количество)
        var items = new List<(int, int)>();
        try {
            foreach (var pair in itemsText.Split(',', StringSplitOptions.RemoveEmptyEntries)) {
                var parts = pair.Split(':');
                items.Add((int.Parse(parts[0].Trim()), int.Parse(parts[1].Trim())));
            }
            var success = _db.Restock(items);
            TempData["Message"] = success ? "Склад успешно пополнен!" : "Ошибка пополнения склада (неверный ID).";
            return RedirectToAction(nameof(Index));
        } catch {
            TempData["Message"] = "Ошибка формата. Используйте: Id:Количество, Id:Количество";
            return View();
        }
    }

    public IActionResult RawParts()
    {
        var rawData = _db.GetRawParts();
        return View(rawData);
    }

    public IActionResult DataTableParts()
    {
        var dataTable = _db.GetPartsDataTable();
        return View(dataTable);
    }

    public async Task<IActionResult> PoolTest()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tasks = new List<Task>();
        
        for (int i = 0; i < 20; i++) {
            tasks.Add(_db.PingDbAsync());
        }
        
        await Task.WhenAll(tasks);
        sw.Stop();
        
        return Content($"Успешно выполнено 20 запросов. Затрачено: {sw.ElapsedMilliseconds} мс. Макс. пул = 5.");
    }

}
