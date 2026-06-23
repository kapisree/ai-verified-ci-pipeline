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
