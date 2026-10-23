using Microsoft.AspNetCore.Mvc;
using ShopApp.Data;
using ShopApp.Models;

namespace ShopApp.Controllers;

public class ProductsController : Controller
{
    private readonly DbHelper _db;

    public ProductsController(DbHelper db)
    {
        _db = db;
    }

    public IActionResult Index()
    {
        var products = _db.GetAllProducts();
        return View(products);
    }

    public IActionResult Details(int id)
    {
        var product = _db.GetProductById(id);
        if (product == null) return NotFound();
        return View(product);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Product product)
    {
        if (ModelState.IsValid)
        {
            _db.AddProduct(product);
            return RedirectToAction(nameof(Index));
        }
        return View(product);
    }

    [HttpPost]
    public IActionResult Buy(int id, int quantity = 1)
    {
        var success = _db.DecreaseStock(id, quantity);
        TempData["Message"] = success
            ? $"Товар куплен! Остаток уменьшен на {quantity}."
            : "Недостаточно товара на складе.";
        return RedirectToAction(nameof(Index));
    }
}