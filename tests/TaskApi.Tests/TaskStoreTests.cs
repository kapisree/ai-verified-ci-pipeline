// tests/TaskApi.Tests/TaskStoreTests.cs
using TaskApi.Storage;
using Xunit;

namespace TaskApi.Tests;

public class TaskStoreTests
{
    [Fact]
    public void Add_ReturnsTaskWithGeneratedIdAndIncomplete()
    {
        var store = new TaskStore();

        var task = store.Add("Buy milk", "2%");

        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal("Buy milk", task.Title);
        Assert.Equal("2%", task.Description);
        Assert.False(task.IsComplete);
    }

    [Fact]
    public void GetAll_ReturnsEmptyList_WhenNoTasksAdded()
    {
        var store = new TaskStore();

        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Get_ReturnsNull_WhenIdNotFound()
    {
        var store = new TaskStore();

        Assert.Null(store.Get(Guid.NewGuid()));
    }

    [Fact]
    public void Get_ReturnsTask_WhenIdExists()
    {
        var store = new TaskStore();
        var added = store.Add("Buy milk", null);

        var found = store.Get(added.Id);

        Assert.NotNull(found);
        Assert.Equal(added.Id, found!.Id);
    }

    [Fact]
    public void Update_ReturnsNull_WhenIdNotFound()
    {
        var store = new TaskStore();

        Assert.Null(store.Update(Guid.NewGuid(), "x", null));
    }

    [Fact]
    public void Update_ChangesTitleAndDescription_WhenIdExists()
    {
        var store = new TaskStore();
        var added = store.Add("Buy milk", null);

        var updated = store.Update(added.Id, "Buy oat milk", "1L");

        Assert.NotNull(updated);
        Assert.Equal("Buy oat milk", updated!.Title);
        Assert.Equal("1L", updated.Description);
    }

    [Fact]
    public void Complete_IsIdempotent()
    {
        var store = new TaskStore();
        var added = store.Add("Buy milk", null);

        store.Complete(added.Id);
        var second = store.Complete(added.Id);

        Assert.NotNull(second);
        Assert.True(second!.IsComplete);
    }

    [Fact]
    public void Delete_ReturnsFalse_WhenIdNotFound()
    {
        var store = new TaskStore();

        Assert.False(store.Delete(Guid.NewGuid()));
    }

    [Fact]
    public void Delete_ReturnsTrueAndRemoves_WhenIdExists()
    {
        var store = new TaskStore();
        var added = store.Add("Buy milk", null);

        var deleted = store.Delete(added.Id);

        Assert.True(deleted);
        Assert.Null(store.Get(added.Id));
    }
}
