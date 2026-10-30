using Dapper;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.Repositories;

public class ProjectRepository
{
    private readonly IDbConnectionFactory _factory;

    public ProjectRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<Project> CreateAsync(string name, int ownerId, string[] tags, string settingsJson)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleAsync<Project>(@"
            INSERT INTO projects (name, owner_id, tags, settings)
            VALUES (@Name, @OwnerId, @Tags, @Settings::jsonb)
            RETURNING *",
            new { Name = name, OwnerId = ownerId, Tags = tags, Settings = settingsJson });
    }

    public async Task<List<Project>> GetByTagAsync(string tag)
    {
        using var conn = _factory.Create();
        return (await conn.QueryAsync<Project>(
            "SELECT * FROM projects WHERE @Tag = ANY(tags) ORDER BY id",
            new { Tag = tag })).ToList();
    }

    public async Task<Project?> GetWithTasksAsync(int projectId)
    {
        using var conn = _factory.Create();
        var dict = new Dictionary<int, Project>();

        await conn.QueryAsync<Project, TaskItem, Project>(@"
            SELECT p.id, p.name, p.owner_id, p.tags, p.settings, p.created_at,
                   t.id, t.project_id, t.title, t.is_done, t.due_date, t.metadata, t.created_at
            FROM projects p
            LEFT JOIN tasks t ON t.project_id = p.id
            WHERE p.id = @Id
            ORDER BY p.id, t.id",
            (project, task) =>
            {
                if (!dict.TryGetValue(project.Id, out var current))
                {
                    current = project;
                    current.Tasks = new List<TaskItem>();
                    dict.Add(current.Id, current);
                }
                if (task is not null && task.Id != 0)
                    current.Tasks.Add(task);
                return current;
            },
            new { Id = projectId },
            splitOn: "id");

        return dict.Values.FirstOrDefault();
    }

    public async Task<(List<Project> Items, int Total)> GetPageAsync(int page, int pageSize)
    {
        using var conn = _factory.Create();
        var offset = (page - 1) * pageSize;

        using var multi = await conn.QueryMultipleAsync(@"
            SELECT COUNT(*) FROM projects;
            SELECT * FROM projects ORDER BY id LIMIT @Limit OFFSET @Offset;",
            new { Limit = pageSize, Offset = offset });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<Project>()).ToList();

        return (items, total);
    }

    // РЕШЕНИЕ: Задание 2
    public async Task UpdateSettingsAsync(int id, string jsonPatch)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            "UPDATE projects SET settings = settings || @Patch::jsonb WHERE id = @Id",
            new { Id = id, Patch = jsonPatch }
        );
    }

    // РЕШЕНИЕ: Задание 3
    public async Task<List<Project>> GetPageKeysetAsync(int lastId, int limit)
    {
        using var conn = _factory.Create();
        return (await conn.QueryAsync<Project>(
            "SELECT * FROM projects WHERE id > @LastId ORDER BY id LIMIT @Limit",
            new { LastId = lastId, Limit = limit }
        )).ToList();
    }

    // РЕШЕНИЕ: Задание 5
    public async Task<List<Project>> GetAllWithTasksAsync()
    {
        using var conn = _factory.Create();
        var dict = new Dictionary<int, Project>();

        await conn.QueryAsync<Project, TaskItem, Project>(@"
            SELECT p.id, p.name, p.owner_id, p.tags, p.settings, p.created_at,
                   t.id, t.project_id, t.title, t.is_done, t.due_date, t.metadata, t.created_at
            FROM projects p
            LEFT JOIN tasks t ON t.project_id = p.id
            ORDER BY p.id, t.id",
            (project, task) =>
            {
                if (!dict.TryGetValue(project.Id, out var current))
                {
                    current = project;
                    current.Tasks = new List<TaskItem>();
                    dict.Add(current.Id, current);
                }
                if (task is not null && task.Id != 0)
                    current.Tasks.Add(task);
                return current;
            },
            splitOn: "id");

        return dict.Values.ToList();
    }

    // РЕШЕНИЕ: Задание 6
    public async Task<Project> CreateWithKickoffAsync(string name, int ownerId, string[] tags, string settingsJson)
    {
        using var conn = _factory.Create();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            var project = await conn.QuerySingleAsync<Project>(@"
                INSERT INTO projects (name, owner_id, tags, settings)
                VALUES (@Name, @OwnerId, @Tags, @Settings::jsonb)
                RETURNING *",
                new { Name = name, OwnerId = ownerId, Tags = tags, Settings = settingsJson },
                transaction: tx);

            var task = await conn.QuerySingleAsync<TaskItem>(@"
                INSERT INTO tasks (project_id, title, metadata)
                VALUES (@ProjectId, 'Kickoff', '{}'::jsonb)
                RETURNING *",
                new { ProjectId = project.Id },
                transaction: tx);

            project.Tasks.Add(task);
            await tx.CommitAsync();
            return project;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}