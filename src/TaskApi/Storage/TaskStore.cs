using System.Collections.Concurrent;
using TaskApi.Models;

namespace TaskApi.Storage;

public class TaskStore
{
    private readonly ConcurrentDictionary<Guid, TaskItem> _tasks = new();

    public TaskItem Add(string title, string? description)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            IsComplete = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _tasks[task.Id] = task;
        return task;
    }

    public IReadOnlyList<TaskItem> GetAll() =>
        _tasks.Values.OrderBy(t => t.CreatedAt).ToList();

    public TaskItem? Get(Guid id) =>
        _tasks.TryGetValue(id, out var task) ? task : null;

    public TaskItem? Update(Guid id, string title, string? description)
    {
        if (!_tasks.TryGetValue(id, out var task))
        {
            return null;
        }

        task.Title = title;
        task.Description = description;
        return task;
    }

    public TaskItem? Complete(Guid id)
    {
        if (!_tasks.TryGetValue(id, out var task))
        {
            return null;
        }

        task.IsComplete = true;
        return task;
    }

    public bool Delete(Guid id) => _tasks.TryRemove(id, out _);
}
