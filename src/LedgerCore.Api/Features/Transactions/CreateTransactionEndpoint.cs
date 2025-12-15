// Projekt: LedgerCore.Api
using Carter;
using LedgerCore.Application.Features.Transactions;
using MediatR; // using do tamtego pliku

public class CreateTransactionEndpoint : ICarterModule // Lub zwykła statyczna metoda extension
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/v1/transactions", async (CreateTransaction.Command command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command);
            return Results.Ok(result);
        }).WithTags("Transactions").
        WithName("CreateTransaction");
    }
}