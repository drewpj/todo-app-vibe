using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Api.Auth;
using TodoApp.Api.Contracts;
using TodoApp.Api.Services;

namespace TodoApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/todos")]
[Produces("application/json")]
public class TodosController(ITodoService todos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TodoDto>>> List([FromQuery] TodoListQuery query, CancellationToken ct) =>
        await todos.ListAsync(User.GetUserId(), query, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TodoDto>> Get(Guid id, CancellationToken ct) =>
        await todos.GetAsync(User.GetUserId(), id, ct);

    [HttpPost]
    [ProducesResponseType<TodoDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TodoDto>> Create(SaveTodoRequest request, CancellationToken ct)
    {
        var created = await todos.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TodoDto>> Update(Guid id, SaveTodoRequest request, CancellationToken ct) =>
        await todos.UpdateAsync(User.GetUserId(), id, request, ct);

    [HttpPatch("{id:guid}/completion")]
    public async Task<ActionResult<TodoDto>> SetCompletion(Guid id, SetCompletionRequest request, CancellationToken ct) =>
        await todos.SetCompletionAsync(User.GetUserId(), id, request.IsCompleted, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await todos.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}
