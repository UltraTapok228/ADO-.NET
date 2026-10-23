using Npgsql;
using ShopApp.Models;

namespace ShopApp.Data;

public class DbHelper
{
    private readonly string _connectionString;

    public DbHelper(string connectionString)
    {
        _connectionString = connectionString;
    }

    // Инициализация схемы и тестовых данных
    public void Initialize()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        // Создание таблицы
        using (var createCmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS products (
                id          SERIAL PRIMARY KEY,
                name        VARCHAR(200) NOT NULL,
                price       NUMERIC(18,2) NOT NULL,
                stock       INTEGER NOT NULL DEFAULT 0,
                description VARCHAR(500)
            )", connection))
        {
            createCmd.ExecuteNonQuery();
        }

        // Проверка наличия данных
        using var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM products", connection);
        var count = Convert.ToInt32(checkCmd.ExecuteScalar());

        if (count == 0)
        {
            using var seedCmd = new NpgsqlCommand(@"
                INSERT INTO products (name, price, stock, description) VALUES
                ('Ноутбук', 59999.00, 10, 'Мощный ноутбук для работы'),
                ('Мышь', 1500.00, 50, 'Беспроводная мышь'),
                ('Клавиатура', 3500.00, 30, 'Механическая клавиатура')", connection);
            seedCmd.ExecuteNonQuery();
        }
    }

    // Получить все товары
    public List<Product> GetAllProducts()
    {
        var products = new List<Product>();
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var cmd = new NpgsqlCommand(
            "SELECT id, name, price, stock, description FROM products ORDER BY id",
            connection);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            products.Add(new Product
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Price = reader.GetDecimal(2),
                Stock = reader.GetInt32(3),
                Description = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return products;
    }

    // Получить один товар
    public Product? GetProductById(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var cmd = new NpgsqlCommand(
            "SELECT id, name, price, stock, description FROM products WHERE id = @id",
            connection);
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Product
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Price = reader.GetDecimal(2),
                Stock = reader.GetInt32(3),
                Description = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }
        return null;
    }

    // Добавить товар
    public void AddProduct(Product product)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var cmd = new NpgsqlCommand(@"
            INSERT INTO products (name, price, stock, description)
            VALUES (@name, @price, @stock, @description)", connection);
        cmd.Parameters.AddWithValue("@name", product.Name);
        cmd.Parameters.AddWithValue("@price", product.Price);
        cmd.Parameters.AddWithValue("@stock", product.Stock);
        cmd.Parameters.AddWithValue("@description", (object?)product.Description ?? DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    // Уменьшить остаток (покупка) — атомарно с проверкой
    public bool DecreaseStock(int productId, int quantity)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var cmd = new NpgsqlCommand(@"
            UPDATE products
            SET stock = stock - @qty
            WHERE id = @id AND stock >= @qty", connection);
        cmd.Parameters.AddWithValue("@id", productId);
        cmd.Parameters.AddWithValue("@qty", quantity);

        return cmd.ExecuteNonQuery() > 0;
    }
}