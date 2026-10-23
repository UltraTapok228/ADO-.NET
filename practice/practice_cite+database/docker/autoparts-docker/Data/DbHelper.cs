using System;
using System.Collections.Generic;
using Npgsql;
using AutoPartsApp.Models;

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

    // 2. Получить все запчасти
    public List<Part> GetAllParts()
    {
        var parts = new List<Part>();
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var cmd = new NpgsqlCommand(
            "SELECT id, name, article, manufacturer, price, stock, description FROM parts ORDER BY id",
            connection);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            parts.Add(new Part
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Article = reader.GetString(2),
                Manufacturer = reader.GetString(3),
                Price = reader.GetDecimal(4),
                Stock = reader.GetInt32(5),
                // Проверка на NULL перед чтением строки
                Description = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }
        return parts;
    }

    // 3. Получить одну запчасть по Id
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

    // 4. Добавить новую запчасть
    public void AddPart(Part part)
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
        // Если описание пустое, передаем в базу явный NULL, а не пустую ссылку
        cmd.Parameters.AddWithValue("@description", (object?)part.Description ?? DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    // 5. Атомарно уменьшить остаток
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
}