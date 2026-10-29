using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace OrderFlow.BuildingBlocks.Errors;

/// <summary>Writes <see cref="AppException"/>s as RFC 9457 problem details with a <c>code</c> field.</summary>
public sealed class AppExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var app = exception switch
        {
            AppException e => e,
            DbUpdateConcurrencyException => new ConflictException("concurrent_update", "Someone else changed this at the same time. Reload and try again."),
            _ => null,
        };
        if (app is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = app.Status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = app.Status, Title = app.Message, Extensions = { ["code"] = app.Code } },
        });
    }
}
