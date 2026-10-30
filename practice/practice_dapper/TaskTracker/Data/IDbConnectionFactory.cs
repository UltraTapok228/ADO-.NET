using Npgsql;

namespace TaskTracker.Data;

public interface IDbConnectionFactory
{
    NpgsqlConnection Create();
}