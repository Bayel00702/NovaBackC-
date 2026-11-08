using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace OnlineStore.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Необработанная ошибка: {Method} {Path}",
            context.Request.Method, context.Request.Path);

        var (status, title) = exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Данные были изменены другим запросом, повторите попытку"),
            DbUpdateException => (StatusCodes.Status409Conflict, "Не удалось сохранить данные из-за конфликта"),
            _ => (StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервера")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // Подробности показываем только при разработке
            Detail = _env.IsDevelopment() ? exception.Message : null
        };

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}