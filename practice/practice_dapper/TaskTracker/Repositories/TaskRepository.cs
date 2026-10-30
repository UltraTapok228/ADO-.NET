using Dapper;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.Repositories;

public class TaskRepository
{
    private readonly IDbConnectionFactory _factory;

    public TaskRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<TaskItem> CreateAsync(int projectId, string title, DateOnly? dueDate, string metadataJson)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleAsync<TaskItem>(@"
            INSERT INTO tasks (project_id, title, due_date, metadata)
            VALUES (@ProjectId, @Title, @DueDate, @Metadata::jsonb)
            RETURNING *",
            new { ProjectId = projectId, Title = title, DueDate = dueDate, Metadata = metadataJson });
    }

    public async Task<TaskItem?> MarkDoneAsync(int taskId)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<TaskItem>(@"
            UPDATE tasks
            SET is_done = true
            WHERE id = @Id AND is_done = false
            RETURNING *",
            new { Id = taskId });
    }

    public async Task BulkInsertAsync(int projectId, IEnumerable<string> titles)
    {
        using var conn = _factory.Create();
        var arr = titles.ToArray();
        await conn.ExecuteAsync(@"
            INSERT INTO tasks (project_id, title)
            SELECT @ProjectId, t FROM unnest(@Titles) AS t",
            new { ProjectId = projectId, Titles = arr });
    }

    public async Task<TaskItem?> DeleteAsync(int taskId)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<TaskItem>(
            "DELETE FROM tasks WHERE id = @Id RETURNING *",
            new { Id = taskId });
    }

    public async Task MarkManyDoneAsync(int projectId)
    {
        using var conn = _factory.Create();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            await conn.ExecuteAsync(@"
                UPDATE tasks
                SET is_done = true
                WHERE project_id = @ProjectId AND is_done = false",
                new { ProjectId = projectId },
                transaction: tx);

            await conn.ExecuteAsync(@"
                INSERT INTO projects (name, owner_id, settings)
                SELECT name || ' (archived)', owner_id, settings
                FROM projects WHERE id = @ProjectId",
                new { ProjectId = projectId },
                transaction: tx);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // РЕШЕНИЕ: Задание 1
    public async Task<List<TaskItem>> GetOverdueAsync()
    {
        using var conn = _factory.Create();
        return (await conn.QueryAsync<TaskItem>(
            "SELECT * FROM tasks WHERE due_date < CURRENT_DATE AND is_done = false"
        )).ToList();
    }

    // РЕШЕНИЕ: Задание 4
    public async Task BulkInsertCopyAsync(int projectId, IEnumerable<string> titles)
    {
        using var conn = _factory.Create();
        await conn.OpenAsync();
        
        await using var writer = await conn.BeginBinaryImportAsync(
            "COPY tasks (project_id, title) FROM STDIN (FORMAT BINARY)");

        foreach (var title in titles)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(projectId, NpgsqlTypes.NpgsqlDbType.Integer);
            await writer.WriteAsync(title, NpgsqlTypes.NpgsqlDbType.Text);
        }

        await writer.CompleteAsync();
    }
}