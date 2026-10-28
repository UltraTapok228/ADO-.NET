using System;
using System.Collections.Generic;
using Npgsql;
using AutoPartsApp.Models;
using System.Data;

namespace AutoPartsApp.Data;

public class DbHelper
{
    private readonly string _connectionString;

    public DbHelper(string connectionString)
    {
        _connectionString = connectionString;
    }

    // 1. Инициализация схемы и сидинг данных
    public void Initialize()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using (var createCmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS parts (
                id           SERIAL PRIMARY KEY,
                name         VARCHAR(200) NOT NULL,
                article      VARCHAR(50)  NOT NULL,
                manufacturer VARCHAR(100) NOT NULL,
                price        NUMERIC(18,2) NOT NULL,
                stock        INTEGER NOT NULL DEFAULT 0,
                description  VARCHAR(500)
            )", connection))
        {
            createCmd.ExecuteNonQuery();
        }

        try {
            using var constraintCmd = new NpgsqlCommand("ALTER TABLE parts ADD CONSTRAINT uq_parts_article UNIQUE (article)", connection);
            constraintCmd.ExecuteNonQuery();
        } catch (PostgresException ex) when (ex.SqlState == "42710") {
            // Игнорируем, если ограничение уже существует
        }

        // 2. И сразу под ним вставляем новый блок для заказов
        using (var createOrdersCmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS orders (
                id         SERIAL PRIMARY KEY,
                part_id    INTEGER NOT NULL,
                quantity   INTEGER NOT NULL,
                total      NUMERIC(18,2) NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )", connection))
        {
            createOrdersCmd.ExecuteNonQuery();
        }

        using var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM parts", connection);
        var count = Convert.ToInt32(checkCmd.ExecuteScalar());

        if (count == 0)
        {
            using var seedCmd = new NpgsqlCommand(@"
                INSERT INTO parts (name, article, manufacturer, price, stock, description) VALUES
                ('Тормозная колодка', 'BC-1234', 'Bosch', 2500.00, 40, 'Передняя, для легковых авто'),
                ('Масляный фильтр', 'OF-5678', 'Mann', 850.00, 120, 'Универсальный'),
                ('Свеча зажигания', 'SP-9012', 'NGK', 450.00, 200, 'Иридиевая')", connection);
            seedCmd.ExecuteNonQuery();
        }
    }

    

    // 2. Получить одну запчасть по Id
    public Part? GetPartById(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var cmd = new NpgsqlCommand(
            "SELECT id, name, article, manufacturer, price, stock, description FROM parts WHERE id = @id",
            connection);
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Part
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Article = reader.GetString(2),
                Manufacturer = reader.GetString(3),
                Price = reader.GetDecimal(4),
                Stock = reader.GetInt32(5),
                Description = reader.IsDBNull(6) ? null : reader.GetString(6)
            };
        }
        return null;
    }

    

    // 3. Атомарно уменьшить остаток
    public bool DecreaseStock(int partId, int quantity)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var cmd = new NpgsqlCommand(@"
            UPDATE parts
            SET stock = stock - @qty
            WHERE id = @id AND stock >= @qty", connection);
            
        cmd.Parameters.AddWithValue("@id", partId);
        cmd.Parameters.AddWithValue("@qty", quantity);

        return cmd.ExecuteNonQuery() > 0;
    }

    // ЗАДАЧА 1: Поиск
    public List<Part> SearchParts(string query)
    {
        var parts = new List<Part>();
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var sql = string.IsNullOrWhiteSpace(query) 
            ? "SELECT id, name, article, manufacturer, price, stock, description FROM parts ORDER BY id"
            : "SELECT id, name, article, manufacturer, price, stock, description FROM parts WHERE name ILIKE @q OR article ILIKE @q ORDER BY id";
            
        using var cmd = new NpgsqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(query))
        {
            cmd.Parameters.AddWithValue("@q", $"%{query}%");
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            parts.Add(new Part
            {
                Id = reader.GetInt32(0), Name = reader.GetString(1), Article = reader.GetString(2),
                Manufacturer = reader.GetString(3), Price = reader.GetDecimal(4), Stock = reader.GetInt32(5),
                Description = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }
        return parts;
    }

    // ЗАДАЧА 2: Удаление
    public bool DeletePart(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var cmd = new NpgsqlCommand("DELETE FROM parts WHERE id = @id", connection);
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    // ЗАДАЧА 3: Статистика
    public (int totalParts, decimal totalValue, decimal avgPrice) GetStats()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var cmd = new NpgsqlCommand("SELECT COUNT(*), COALESCE(SUM(price*stock),0), COALESCE(AVG(price),0) FROM parts", connection);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return (reader.GetInt32(0), reader.GetDecimal(1), reader.GetDecimal(2));
        }
        return (0, 0, 0);
    }

    // ЗАДАЧА 4: Оформление заказа в транзакции
    public bool PlaceOrder(int partId, int quantity)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            // Чтение цены и остатка
            using var checkCmd = new NpgsqlCommand("SELECT price, stock FROM parts WHERE id = @id", connection, tx);
            checkCmd.Parameters.AddWithValue("@id", partId);
            decimal price = 0; int stock = 0;
            using (var reader = checkCmd.ExecuteReader())
            {
                if (reader.Read()) { price = reader.GetDecimal(0); stock = reader.GetInt32(1); }
                else return false;
            }
            if (stock < quantity) return false;

            // Списываем со склада
            using var updateCmd = new NpgsqlCommand("UPDATE parts SET stock = stock - @qty WHERE id = @id", connection, tx);
            updateCmd.Parameters.AddWithValue("@id", partId);
            updateCmd.Parameters.AddWithValue("@qty", quantity);
            updateCmd.ExecuteNonQuery();

            // Создание заказа
            using var insertCmd = new NpgsqlCommand("INSERT INTO orders (part_id, quantity, total) VALUES (@pid, @qty, @total)", connection, tx);
            insertCmd.Parameters.AddWithValue("@pid", partId);
            insertCmd.Parameters.AddWithValue("@qty", quantity);
            insertCmd.Parameters.AddWithValue("@total", price * quantity);
            insertCmd.ExecuteNonQuery();

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            return false;
        }
    }

    // ЗАДАЧА 5: Массовый завоз товара
    public bool Restock(List<(int PartId, int AddQty)> items)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            foreach (var item in items)
            {
                using var cmd = new NpgsqlCommand("UPDATE parts SET stock = stock + @qty WHERE id = @id", connection, tx);
                cmd.Parameters.AddWithValue("@id", item.PartId);
                cmd.Parameters.AddWithValue("@qty", item.AddQty);
                if (cmd.ExecuteNonQuery() == 0) 
                {
                    tx.Rollback();
                    return false;
                }
            }
            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            return false;
        }
    }

    // ЗАДАЧА 8: Асинхронное чтение
    public async Task<List<Part>> GetAllPartsAsync()
    {
        var parts = new List<Part>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT id, name, article, manufacturer, price, stock, description FROM parts ORDER BY id", connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            parts.Add(new Part
            {
                Id = reader.GetInt32(0), Name = reader.GetString(1), Article = reader.GetString(2),
                Manufacturer = reader.GetString(3), Price = reader.GetDecimal(4), Stock = reader.GetInt32(5),
                Description = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }
        return parts;
    }

    // ЗАДАЧА 9: Обработка уникального ограничения (SqlState 23505)
    public (bool Ok, string? Error) TryAddPart(Part part)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO parts (name, article, manufacturer, price, stock, description)
            VALUES (@name, @article, @manufacturer, @price, @stock, @description)", connection);
            
        cmd.Parameters.AddWithValue("@name", part.Name);
        cmd.Parameters.AddWithValue("@article", part.Article);
        cmd.Parameters.AddWithValue("@manufacturer", part.Manufacturer);
        cmd.Parameters.AddWithValue("@price", part.Price);
        cmd.Parameters.AddWithValue("@stock", part.Stock);
        cmd.Parameters.AddWithValue("@description", (object?)part.Description ?? DBNull.Value);

        try {
            cmd.ExecuteNonQuery();
            return (true, null);
        } catch (PostgresException ex) when (ex.SqlState == "23505") {
            return (false, "Запчасть с таким артикулом уже существует");
        }
    }

    // ЗАДАЧА 6: Сырое чтение в Dictionary
    public List<Dictionary<string, object?>> GetRawParts()
    {
        var list = new List<Dictionary<string, object?>>();
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var cmd = new NpgsqlCommand("SELECT * FROM parts", connection);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++) {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            list.Add(row);
        }
        return list;
    }

    // ЗАДАЧА 7: DataTable через NpgsqlDataAdapter
    public DataTable GetPartsDataTable()
    {
        var dt = new DataTable();
        using var connection = new NpgsqlConnection(_connectionString);
        using var cmd = new NpgsqlCommand("SELECT * FROM parts", connection);
        using var adapter = new NpgsqlDataAdapter(cmd);
        adapter.Fill(dt);
        return dt;
    }

    // Вспомогательный метод для теста пула соединений (Задача 10)
    public async Task PingDbAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT 1", connection);
        await cmd.ExecuteScalarAsync();
    }

}