# AI-Verified CI Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a self-contained demo repo with a small .NET Task API, a written `SPEC.md`, and a GitHub Actions pipeline that runs Claude Code spec-compliance review, tests, lint/analyzers, and a security scan as required PR checks.

**Architecture:** ASP.NET Core 8 minimal API (`src/TaskApi`) backed by an in-memory `ConcurrentDictionary` store, covered by xUnit integration tests (`tests/TaskApi.Tests`) using `WebApplicationFactory`. A GitHub Actions workflow (`.github/workflows/verified-ci.yml`) runs four parallel jobs on every PR: `test`, `lint`, `security` (Trivy), and `claude-spec-review` (Anthropic's `claude-code-action` reading `SPEC.md` + the PR diff). Branch protection (documented, not code) makes all four required.

**Tech Stack:** .NET 8 (minimal API), xUnit, `Microsoft.AspNetCore.Mvc.Testing`, GitHub Actions, `dotnet format`, Roslyn analyzers, Trivy, `claude-code-action`.

## Global Constraints

- Target framework: `net8.0` (matches `actions/setup-dotnet` LTS and the locally installed `Microsoft.AspNetCore.App 8.0.28` runtime).
- In-memory storage only — no database, no persistence across restarts.
- Every mutating endpoint (`POST /tasks`, `PUT /tasks/{id}`) validates `title`: required, 1–200 chars after trim; on failure returns `400` with body `{ "error": "<message>" }`, never an unhandled exception.
- No endpoint may return stack traces or internal exception details in a response body.
- All status codes and shapes match `SPEC.md` exactly (written in Task 9).

---

## File Structure

```
TaskApi.sln
src/TaskApi/TaskApi.csproj
src/TaskApi/Program.cs
src/TaskApi/Models/TaskItem.cs
src/TaskApi/Models/TaskRequest.cs
src/TaskApi/Storage/TaskStore.cs
src/TaskApi/Validation/TaskValidator.cs
tests/TaskApi.Tests/TaskApi.Tests.csproj
tests/TaskApi.Tests/TaskStoreTests.cs
tests/TaskApi.Tests/TasksEndpointsTests.cs
.editorconfig
SPEC.md
.github/workflows/verified-ci.yml
.github/claude-review-prompt.md
README.md
.gitignore
```

---

### Task 1: Solution and project scaffolding

**Files:**
- Create: `TaskApi.sln`
- Create: `src/TaskApi/TaskApi.csproj`
- Create: `src/TaskApi/Program.cs`
- Create: `tests/TaskApi.Tests/TaskApi.Tests.csproj`
- Create: `.gitignore`

**Interfaces:**
- Produces: a `dotnet build`-able solution with a minimal API project (`TaskApi`, namespace `TaskApi`) exposing a `public partial class Program` so `WebApplicationFactory<Program>` can target it from the test project, and an xUnit test project referencing it.

- [ ] **Step 1: Create the solution and projects**

```bash
cd /mnt/d/Kapilas/myCode/claude/ai-verified-ci-pipeline
dotnet new sln -n TaskApi
dotnet new web -n TaskApi -o src/TaskApi -f net8.0
dotnet new xunit -n TaskApi.Tests -o tests/TaskApi.Tests -f net8.0
dotnet sln add src/TaskApi/TaskApi.csproj tests/TaskApi.Tests/TaskApi.Tests.csproj
dotnet add tests/TaskApi.Tests/TaskApi.Tests.csproj reference src/TaskApi/TaskApi.csproj
dotnet add tests/TaskApi.Tests/TaskApi.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing --version 8.0.8
```

- [ ] **Step 2: Replace `src/TaskApi/Program.cs` with a minimal, testable skeleton**

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "TaskApi running");

app.Run();

public partial class Program { }
```

- [ ] **Step 3: Write `.gitignore`**

```
bin/
obj/
*.user
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build TaskApi.sln`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add TaskApi.sln src/TaskApi tests/TaskApi.Tests .gitignore
git commit -m "Scaffold TaskApi solution and test project"
```

---

### Task 2: Task model and in-memory store (TDD)

**Files:**
- Create: `src/TaskApi/Models/TaskItem.cs`
- Create: `src/TaskApi/Storage/TaskStore.cs`
- Test: `tests/TaskApi.Tests/TaskStoreTests.cs`

**Interfaces:**
- Produces: `TaskItem { Guid Id; string Title; string? Description; bool IsComplete; DateTimeOffset CreatedAt; }` and `TaskStore` with methods `TaskItem Add(string title, string? description)`, `IReadOnlyList<TaskItem> GetAll()`, `TaskItem? Get(Guid id)`, `TaskItem? Update(Guid id, string title, string? description)`, `TaskItem? Complete(Guid id)`, `bool Delete(Guid id)`. These are consumed by Task 3–7's endpoints.

- [ ] **Step 1: Write the failing tests**

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TaskApi.Tests --filter TaskStoreTests`
Expected: compile error — `TaskStore` and `TaskItem` do not exist yet.

- [ ] **Step 3: Implement `TaskItem`**

```csharp
// src/TaskApi/Models/TaskItem.cs
namespace TaskApi.Models;

public class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsComplete { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

- [ ] **Step 4: Implement `TaskStore`**

```csharp
// src/TaskApi/Storage/TaskStore.cs
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
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/TaskApi.Tests --filter TaskStoreTests`
Expected: all 9 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/TaskApi/Models/TaskItem.cs src/TaskApi/Storage/TaskStore.cs tests/TaskApi.Tests/TaskStoreTests.cs
git commit -m "Add TaskItem model and in-memory TaskStore with tests"
```

---

### Task 3: Title validation helper (TDD)

**Files:**
- Create: `src/TaskApi/Validation/TaskValidator.cs`
- Test: add to `tests/TaskApi.Tests/TaskStoreTests.cs`'s neighbor — create `tests/TaskApi.Tests/TaskValidatorTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `static class TaskValidator` with `static string? ValidateTitle(string? title)` returning `null` when valid, or an error message string when invalid. Consumed by the `POST`/`PUT` endpoints in Task 4 and Task 6.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/TaskApi.Tests/TaskValidatorTests.cs
using TaskApi.Validation;
using Xunit;

namespace TaskApi.Tests;

public class TaskValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateTitle_ReturnsError_WhenTitleMissingOrBlank(string? title)
    {
        var error = TaskValidator.ValidateTitle(title);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateTitle_ReturnsError_WhenTitleExceeds200Characters()
    {
        var longTitle = new string('a', 201);

        var error = TaskValidator.ValidateTitle(longTitle);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateTitle_ReturnsNull_WhenTitleIsValid()
    {
        var error = TaskValidator.ValidateTitle("Buy milk");

        Assert.Null(error);
    }

    [Fact]
    public void ValidateTitle_ReturnsNull_WhenTitleIsExactly200Characters()
    {
        var title = new string('a', 200);

        var error = TaskValidator.ValidateTitle(title);

        Assert.Null(error);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TaskApi.Tests --filter TaskValidatorTests`
Expected: compile error — `TaskValidator` does not exist yet.

- [ ] **Step 3: Implement `TaskValidator`**

```csharp
// src/TaskApi/Validation/TaskValidator.cs
namespace TaskApi.Validation;

public static class TaskValidator
{
    public static string? ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Title is required.";
        }

        if (title.Trim().Length > 200)
        {
            return "Title must be 200 characters or fewer.";
        }

        return null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/TaskApi.Tests --filter TaskValidatorTests`
Expected: all 4 tests (6 cases incl. `[Theory]` data) pass.

- [ ] **Step 5: Commit**

```bash
git add src/TaskApi/Validation/TaskValidator.cs tests/TaskApi.Tests/TaskValidatorTests.cs
git commit -m "Add TaskValidator for title validation"
```

---

### Task 4: `POST /tasks` and `GET /tasks`, `GET /tasks/{id}` endpoints (TDD)

**Files:**
- Create: `src/TaskApi/Models/TaskRequest.cs`
- Modify: `src/TaskApi/Program.cs`
- Test: `tests/TaskApi.Tests/TasksEndpointsTests.cs`

**Interfaces:**
- Consumes: `TaskStore` (Task 2), `TaskValidator.ValidateTitle` (Task 3).
- Produces: registers `TaskStore` as a singleton in DI; establishes the `TasksEndpointsTests` test class and its `WebApplicationFactory<Program>`-based `HttpClient` fixture pattern, reused and extended by Tasks 5–8.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/TaskApi.Tests/TasksEndpointsTests.cs
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
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TaskApi.Tests --filter TasksEndpointsTests`
Expected: compile error — `TaskRequest` missing; 404s once it compiles (no routes registered yet).

- [ ] **Step 3: Implement `TaskRequest`**

```csharp
// src/TaskApi/Models/TaskRequest.cs
namespace TaskApi.Models;

public class TaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
```

- [ ] **Step 4: Wire up the endpoints in `Program.cs`**

```csharp
// src/TaskApi/Program.cs
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

app.Run();

public partial class Program { }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/TaskApi.Tests --filter TasksEndpointsTests`
Expected: all 5 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/TaskApi/Models/TaskRequest.cs src/TaskApi/Program.cs tests/TaskApi.Tests/TasksEndpointsTests.cs
git commit -m "Add POST /tasks, GET /tasks, GET /tasks/{id} endpoints"
```

---

### Task 5: `PUT /tasks/{id}` endpoint (TDD)

**Files:**
- Modify: `src/TaskApi/Program.cs`
- Modify: `tests/TaskApi.Tests/TasksEndpointsTests.cs`

**Interfaces:**
- Consumes: `TaskStore.Update` (Task 2), `TaskValidator.ValidateTitle` (Task 3).

- [ ] **Step 1: Add failing tests** (append to `TasksEndpointsTests.cs`, inside the class)

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TaskApi.Tests --filter TasksEndpointsTests`
Expected: the 3 new tests fail with 404 (no `PUT` route registered).

- [ ] **Step 3: Add the `PUT` route in `Program.cs`** (insert after the `GET /tasks/{id:guid}` route, before `app.Run();`)

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/TaskApi.Tests --filter TasksEndpointsTests`
Expected: all 8 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/TaskApi/Program.cs tests/TaskApi.Tests/TasksEndpointsTests.cs
git commit -m "Add PUT /tasks/{id} endpoint"
```

---

### Task 6: `POST /tasks/{id}/complete` and `DELETE /tasks/{id}` endpoints (TDD)

**Files:**
- Modify: `src/TaskApi/Program.cs`
- Modify: `tests/TaskApi.Tests/TasksEndpointsTests.cs`

**Interfaces:**
- Consumes: `TaskStore.Complete`, `TaskStore.Delete` (Task 2).

- [ ] **Step 1: Add failing tests** (append to `TasksEndpointsTests.cs`, inside the class)

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TaskApi.Tests --filter TasksEndpointsTests`
Expected: the 5 new tests fail with 404 (no `complete`/`DELETE` routes registered).

- [ ] **Step 3: Add the routes in `Program.cs`** (insert after the `PUT` route, before `app.Run();`)

```csharp
app.MapPost("/tasks/{id:guid}/complete", (Guid id, TaskStore store) =>
{
    var task = store.Complete(id);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

app.MapDelete("/tasks/{id:guid}", (Guid id, TaskStore store) =>
{
    var deleted = store.Delete(id);
    return deleted ? Results.NoContent() : Results.NotFound();
});
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/TaskApi.Tests --filter TasksEndpointsTests`
Expected: all 13 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/TaskApi/Program.cs tests/TaskApi.Tests/TasksEndpointsTests.cs
git commit -m "Add complete and delete endpoints"
```

---

### Task 7: No-stack-trace error handling (TDD)

**Files:**
- Modify: `src/TaskApi/Program.cs`
- Modify: `tests/TaskApi.Tests/TasksEndpointsTests.cs`

**Interfaces:**
- Produces: a global exception handler returning a generic `500` JSON body without exception details, satisfying the spec's "no stack traces leaked" rule for any unexpected error.

- [ ] **Step 1: Add a route that deliberately throws, and a test asserting the response is sanitized** (append to `TasksEndpointsTests.cs`, inside the class)

```csharp
    [Fact]
    public async Task UnhandledException_Returns500WithoutStackTrace()
    {
        var response = await _client.GetAsync("/__boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("at TaskApi.", body);
        Assert.DoesNotContain("System.Exception", body);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/TaskApi.Tests --filter UnhandledException_Returns500WithoutStackTrace`
Expected: 404 (the `/__boom` route does not exist yet).

- [ ] **Step 3: Add the exception handler and the test-only throwing route in `Program.cs`** (insert the handler setup right after `var app = builder.Build();`, and the throwing route alongside the other `app.Map...` calls)

```csharp
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    });
});

app.MapGet("/__boom", () => { throw new InvalidOperationException("boom"); });
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/TaskApi.Tests`
Expected: all 14 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/TaskApi/Program.cs tests/TaskApi.Tests/TasksEndpointsTests.cs
git commit -m "Add global exception handler that suppresses stack traces"
```

---

### Task 8: `SPEC.md`

**Files:**
- Create: `SPEC.md`

**Interfaces:**
- Produces: the contract document `claude-spec-review` (Task 11) checks PRs against. Must match the endpoints and rules implemented in Tasks 2–7 exactly.

- [ ] **Step 1: Write `SPEC.md`**

```markdown
# TaskApi Specification

TaskApi is an in-memory task management API. This document is the contract
that all pull requests must comply with.

## Endpoints

### POST /tasks
Create a task.

- Request body: `{ "title": string, "description"?: string }`
- `title` is required: 1-200 characters after trimming whitespace.
  - Missing, empty, whitespace-only, or over 200 characters → `400`
    with body `{ "error": "<message>" }`.
- On success → `201 Created` with the created task:
  `{ "id": guid, "title": string, "description": string|null,
  "isComplete": false, "createdAt": datetime }`.

### GET /tasks
List all tasks.

- → `200 OK` with a JSON array of tasks (empty array if none exist).

### GET /tasks/{id}
Get one task by id.

- → `200 OK` with the task if found.
- → `404 Not Found` if no task has that id.

### PUT /tasks/{id}
Update a task's `title` and `description`.

- Request body: same shape and validation rules as `POST /tasks`.
- → `200 OK` with the updated task if found and valid.
- → `404 Not Found` if no task has that id.
- → `400 Bad Request` if the title is invalid.

### POST /tasks/{id}/complete
Mark a task complete. Idempotent: calling this twice on the same task is
not an error.

- → `200 OK` with the updated task if found.
- → `404 Not Found` if no task has that id.

### DELETE /tasks/{id}
Delete a task.

- → `204 No Content` if found and deleted.
- → `404 Not Found` if no task has that id.

## Non-Functional Rules

1. Every mutating endpoint (`POST /tasks`, `PUT /tasks/{id}`) must validate
   `title` and return `400` with a body describing the validation error on
   failure. It must never allow an unhandled exception to escape for bad
   input.
2. No endpoint may return a stack trace or other internal exception detail
   in a response body. Unexpected errors must return a generic `500` with
   a sanitized error message.
```

- [ ] **Step 2: Verify the spec matches the implementation**

Run: `dotnet test tests/TaskApi.Tests`
Expected: all 14 tests still pass (no code changed in this task — this step is a sanity check that the spec describes behavior the tests already lock in).

- [ ] **Step 3: Commit**

```bash
git add SPEC.md
git commit -m "Add SPEC.md as the contract for spec-compliance review"
```

---

### Task 9: Lint configuration (`dotnet format` + analyzers)

**Files:**
- Create: `.editorconfig`
- Modify: `src/TaskApi/TaskApi.csproj`

**Interfaces:**
- Produces: a `dotnet format --verify-no-changes` baseline that passes cleanly and analyzer warnings promoted to build errors, consumed by the `lint` CI job in Task 11.

- [ ] **Step 1: Write `.editorconfig`**

```ini
root = true

[*.cs]
indent_style = space
indent_size = 4
end_of_line = lf
insert_final_newline = true
charset = utf-8-bom

dotnet_diagnostic.CA1062.severity = suggestion
```

- [ ] **Step 2: Enable analyzers as build errors in `TaskApi.csproj`**

Open `src/TaskApi/TaskApi.csproj` and add inside the existing `<PropertyGroup>`:

```xml
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<AnalysisLevel>latest</AnalysisLevel>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

- [ ] **Step 3: Run formatting and fix any reported issues**

Run: `dotnet format TaskApi.sln`
Then run: `dotnet format TaskApi.sln --verify-no-changes`
Expected: second command exits `0` with no output (clean).

- [ ] **Step 4: Run a full build to confirm analyzers don't fail it**

Run: `dotnet build TaskApi.sln`
Expected: `Build succeeded.` with 0 errors, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add .editorconfig src/TaskApi/TaskApi.csproj
git commit -m "Add lint configuration: editorconfig and analyzer-as-error build settings"
```

---

### Task 10: GitHub Actions workflow — `test`, `lint`, `security` jobs

**Files:**
- Create: `.github/workflows/verified-ci.yml`

**Interfaces:**
- Produces: three of the four required PR status checks (`test`, `lint`, `security`); Task 11 adds the fourth (`claude-spec-review`) to the same file.

- [ ] **Step 1: Write `.github/workflows/verified-ci.yml` with the three jobs**

```yaml
name: Verified CI

on:
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"
      - run: dotnet test TaskApi.sln

  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"
      - run: dotnet format TaskApi.sln --verify-no-changes
      - run: dotnet build TaskApi.sln --warnaserror

  security:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: aquasecurity/trivy-action@0.24.0
        with:
          scan-type: fs
          scan-ref: .
          severity: HIGH,CRITICAL
          exit-code: "1"
```

- [ ] **Step 2: Validate the YAML is well-formed**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/verified-ci.yml'))"`
Expected: no output, exit code `0`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/verified-ci.yml
git commit -m "Add test, lint, and security jobs to verified-ci workflow"
```

---

### Task 11: Claude spec-compliance review job

**Files:**
- Modify: `.github/workflows/verified-ci.yml`
- Create: `.github/claude-review-prompt.md`

**Interfaces:**
- Consumes: `SPEC.md` (Task 8).
- Produces: the fourth required check, `claude-spec-review`.

- [ ] **Step 1: Write `.github/claude-review-prompt.md`**

```markdown
You are reviewing a pull request for compliance with the contract in
`SPEC.md` at the root of this repository.

1. Read `SPEC.md` in full.
2. Read the diff for this pull request.
3. Determine whether the diff introduces any behavior that contradicts
   `SPEC.md` — wrong status codes, missing validation, changed response
   shapes, broken idempotency, leaked exception details, or any other
   deviation from a stated rule.
4. Post a PR comment listing each violation found, quoting the specific
   `SPEC.md` clause it breaks. If there are no violations, post a comment
   confirming the diff is spec-compliant.
5. If you found at least one violation, end your final message with the
   exact line `SPEC_COMPLIANCE: FAIL`. If you found none, end with the
   exact line `SPEC_COMPLIANCE: PASS`.
```

- [ ] **Step 2: Add the `claude-spec-review` job to `.github/workflows/verified-ci.yml`** (append after the `security` job)

```yaml
  claude-spec-review:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - uses: anthropics/claude-code-action@v1
        with:
          anthropic_api_key: ${{ secrets.ANTHROPIC_API_KEY }}
          prompt_file: .github/claude-review-prompt.md
          fail_on: "SPEC_COMPLIANCE: FAIL"
```

- [ ] **Step 3: Validate the YAML is well-formed**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/verified-ci.yml'))"`
Expected: no output, exit code `0`.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/verified-ci.yml .github/claude-review-prompt.md
git commit -m "Add Claude spec-compliance review job to verified-ci workflow"
```

---

### Task 12: README — setup, branch protection, and the failure-case demo

**Files:**
- Create: `README.md`

**Interfaces:**
- Consumes: all prior tasks (describes the whole repo).

- [ ] **Step 1: Write `README.md`**

```markdown
# AI-Verified CI Pipeline (Demo)

A small ASP.NET Core "TaskApi" plus a GitHub Actions pipeline that blocks
merge unless a pull request passes four required checks: a Claude Code
spec-compliance review, tests, lint/analyzers, and a security scan. This
is a demo of the pattern, not a production service.

## What's here

- `SPEC.md` — the written contract `claude-spec-review` checks PRs
  against.
- `src/TaskApi` — the sample app: an in-memory task CRUD API.
- `tests/TaskApi.Tests` — xUnit tests mirroring `SPEC.md`.
- `.github/workflows/verified-ci.yml` — the four-job pipeline.
- `.github/claude-review-prompt.md` — instructions given to Claude for
  the spec-compliance job.

## One-time setup

1. Add an `ANTHROPIC_API_KEY` repository secret (Settings → Secrets and
   variables → Actions → New repository secret). Required for the
   `claude-spec-review` job.
2. Enable branch protection on `main` (Settings → Branches → Add rule):
   - Require status checks to pass before merging.
   - Select all four checks: `test`, `lint`, `security`,
     `claude-spec-review`.
   - Require branches to be up to date before merging.

## Running locally

```bash
dotnet test TaskApi.sln
dotnet format TaskApi.sln --verify-no-changes
```

## Demonstrating a blocked merge

1. Branch from `main`: `git checkout -b demo/spec-violation`.
2. Edit `src/TaskApi/Program.cs` so the `complete` route is no longer
   idempotent in a way that still compiles and still passes the existing
   tests — for example, change `POST /tasks/{id}/complete` to return
   `409 Conflict` on the second call instead of `200`. This contradicts
   the idempotency rule in `SPEC.md` but isn't covered by every test, so
   it can slip past `test`/`lint`/`security`.
3. Push the branch and open a PR against `main`.
4. Watch the `claude-spec-review` check: it reads `SPEC.md`, finds the
   idempotency rule violated, posts a PR comment quoting the rule, and
   fails the check — blocking merge even though the other three checks
   are green.
5. Revert the change to see the same PR pass once it's spec-compliant
   again.
```

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "Add README with setup instructions and failure-case demo walkthrough"
```
