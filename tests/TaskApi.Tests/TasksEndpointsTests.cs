using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TaskApi.Models;
using Xunit;

namespace TaskApi.Tests;

public class TasksEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TasksEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostTasks_Returns201WithCreatedTask_WhenTitleValid()
    {
        var response = await _client.PostAsJsonAsync("/tasks", new TaskRequest { Title = "Buy milk", Description = "2%" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(body);
        Assert.Equal("Buy milk", body!.Title);
        Assert.False(body.IsComplete);
    }

    [Fact]
    public async Task PostTasks_Returns400_WhenTitleMissing()
    {
        var response = await _client.PostAsJsonAsync("/tasks", new TaskRequest { Title = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTasks_ReturnsEmptyArray_WhenNoTasksCreatedYet()
    {
        var response = await _client.GetAsync("/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTaskById_Returns200WithTask_WhenTaskExists()
    {
        var created = await (await _client.PostAsJsonAsync("/tasks", new TaskRequest { Title = "Buy milk" }))
            .Content.ReadFromJsonAsync<TaskItem>();

        var response = await _client.GetAsync($"/tasks/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTaskById_Returns404_WhenTaskDoesNotExist()
    {
        var response = await _client.GetAsync($"/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutTaskById_Returns200WithUpdatedTask_WhenTaskExistsAndTitleValid()
    {
        var created = await (await _client.PostAsJsonAsync("/tasks", new TaskRequest { Title = "Buy milk" }))
            .Content.ReadFromJsonAsync<TaskItem>();

        var response = await _client.PutAsJsonAsync($"/tasks/{created!.Id}", new TaskRequest { Title = "Buy oat milk", Description = "1L" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.Equal("Buy oat milk", body!.Title);
    }

    [Fact]
    public async Task PutTaskById_Returns404_WhenTaskDoesNotExist()
    {
        var response = await _client.PutAsJsonAsync($"/tasks/{Guid.NewGuid()}", new TaskRequest { Title = "Anything" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutTaskById_Returns400_WhenTitleInvalid()
    {
        var created = await (await _client.PostAsJsonAsync("/tasks", new TaskRequest { Title = "Buy milk" }))
            .Content.ReadFromJsonAsync<TaskItem>();

        var response = await _client.PutAsJsonAsync($"/tasks/{created!.Id}", new TaskRequest { Title = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostComplete_Returns200_WhenTaskExists()
    {
        var created = await (await _client.PostAsJsonAsync("/tasks", new TaskRequest { Title = "Buy milk" }))
            .Content.ReadFromJsonAsync<TaskItem>();

        var response = await _client.PostAsync($"/tasks/{created!.Id}/complete", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.True(body!.IsComplete);
    }

    [Fact]
    public async Task PostComplete_IsIdempotent_WhenCalledTwice()
    {
        var created = await (await _client.PostAsJsonAsync("/tasks", new TaskRequest { Title = "Buy milk" }))
            .Content.ReadFromJsonAsync<TaskItem>();

        await _client.PostAsync($"/tasks/{created!.Id}/complete", content: null);
        var second = await _client.PostAsync($"/tasks/{created.Id}/complete", content: null);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task PostComplete_Returns404_WhenTaskDoesNotExist()
    {
        var response = await _client.PostAsync($"/tasks/{Guid.NewGuid()}/complete", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTaskById_Returns204_WhenTaskExists()
    {
        var created = await (await _client.PostAsJsonAsync("/tasks", new TaskRequest { Title = "Buy milk" }))
            .Content.ReadFromJsonAsync<TaskItem>();

        var response = await _client.DeleteAsync($"/tasks/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTaskById_Returns404_WhenTaskDoesNotExist()
    {
        var response = await _client.DeleteAsync($"/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnhandledException_Returns500WithoutStackTrace()
    {
        var response = await _client.GetAsync("/__boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("at TaskApi.", body);
        Assert.DoesNotContain("System.Exception", body);
    }
}
