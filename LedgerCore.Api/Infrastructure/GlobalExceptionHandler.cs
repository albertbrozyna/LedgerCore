using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LedgerCore.Api.Infrastructure
{
    public class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var statusCode = exception switch
            {
                ApplicationException => StatusCodes.Status400BadRequest,
                BadHttpRequestException => StatusCodes.Status400BadRequest,

                _ => StatusCodes.Status500InternalServerError
            };

            httpContext.Response.StatusCode = statusCode;

            var problemDetailsContext = new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = {
                    Title = "An exception occcured while processing exception",
                    Detail = exception.Message,
                    Type = exception.GetType().Name,
                    Status = statusCode
                }
            };

            await problemDetailsService.TryWriteAsync(problemDetailsContext);


            return true;
        }
    }
}
