using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LedgerCore.Api.Infrastructure
{
    public class ValidationExceptionHandler : IExceptionHandler

    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if(exception is not ValidationException validationException)
            {
                return false;
            }

            // 2. Ustawienie kodu HTTP 400 (Bad Request)
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            // 3. Konwersja błędów FluentValidation na format ProblemDetails
            // Grupujemy błędy po nazwie pola (np. "Email": ["Pusty", "Zły format"])
            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            // 4. Tworzenie obiektu odpowiedzi
            // Używamy ValidationProblemDetails, bo ma wbudowane pole "Errors"
            var problemDetails = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "One or more validation errors occurred.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            };

            // 5. Wysłanie odpowiedzi JSON do klienta
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            // 6. Zwracamy true -> Mówimy rurociągowi: "Obsłużyłem to, koniec pracy".
            return true;
        }
    }
}
