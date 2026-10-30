using Dapper;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.Repositories;

public class UserRepository
{
    private readonly IDbConnectionFactory _factory;

    public UserRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<User> CreateAsync(string userName, string email)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleAsync<User>(@"
            INSERT INTO users (user_name, email)
            VALUES (@UserName, @Email)
            RETURNING id, user_name, email, created_at",
            new { UserName = userName, Email = email });
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @Id",
            new { Id = id });
    }

    public async Task<List<User>> GetAllAsync()
    {
        using var conn = _factory.Create();
        return (await conn.QueryAsync<User>("SELECT * FROM users ORDER BY id")).ToList();
    }

    public async Task<User> UpsertAsync(User user)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleAsync<User>(@"
            INSERT INTO users (id, user_name, email)
            VALUES (@Id, @UserName, @Email)
            ON CONFLICT (id) DO UPDATE
            SET user_name = EXCLUDED.user_name,
                email     = EXCLUDED.email
            RETURNING *",
            user);
    }
}