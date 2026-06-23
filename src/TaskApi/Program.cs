using TaskApi.Models;
using TaskApi.Storage;
using TaskApi.Validation;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TaskStore>();
var app = builder.Build();

app.MapGet("/", () => "TaskApi running");

app.MapPost("/tasks", (TaskRequest request, TaskStore store) =>
{
    var error = TaskValidator.ValidateTitle(request.Title);
    if (error is not null)
    {
        return Results.BadRequest(new { error });
    }

    var task = store.Add(request.Title.Trim(), request.Description);
    return Results.Created($"/tasks/{task.Id}", task);
});

app.MapGet("/tasks", (TaskStore store) => Results.Ok(store.GetAll()));

app.MapGet("/tasks/{id:guid}", (Guid id, TaskStore store) =>
{
    var task = store.Get(id);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

app.MapPut("/tasks/{id:guid}", (Guid id, TaskRequest request, TaskStore store) =>
{
    var error = TaskValidator.ValidateTitle(request.Title);
    if (error is not null)
    {
        return Results.BadRequest(new { error });
    }

    var task = store.Update(id, request.Title.Trim(), request.Description);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

app.Run();

public partial class Program { }
