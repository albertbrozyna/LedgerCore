using LedgerCore.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace LedgerCore.Api.Infrastructure
{
    public class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (statusCode, title) = exception switch
            {
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Brak autoryzacji"),
                DomainException => (StatusCodes.Status400BadRequest, "Operacja niedozwolona"),
                ArgumentException => (StatusCodes.Status400BadRequest, "Nieprawidłowe dane"),
                BadHttpRequestException => (StatusCodes.Status400BadRequest, "Nieprawidłowe żądanie"),
                _ => (StatusCodes.Status500InternalServerError, "Wystąpił nieoczekiwany błąd")
            };

            if (statusCode == 500) logger.LogError(exception, "Nieobsłużony wyjątek");

            httpContext.Response.StatusCode = statusCode;
            await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails =
                {
                    Title = title,
                    // Szczegóły błędów 500 nie wyciekają do klienta
                    Detail = statusCode == 500 ? "Sprawdź logi aplikacji." : exception.Message,
                    Status = statusCode
                }
            });
            return true;
        }
    }
}
