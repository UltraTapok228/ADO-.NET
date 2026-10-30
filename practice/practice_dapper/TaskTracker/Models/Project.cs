namespace TaskTracker.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int OwnerId { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string Settings { get; set; } = "{}";  // JSONB как строка
    public DateTime CreatedAt { get; set; }
    public List<TaskItem> Tasks { get; set; } = new();
}