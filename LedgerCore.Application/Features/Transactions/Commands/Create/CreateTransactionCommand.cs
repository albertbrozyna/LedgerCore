// 1. Główna koperta z danymi transakcji
using LedgerCore.Domain.Enums;
using MediatR;


public static partial class CreateTransaction
{
    public record NewEntryDto(Guid AccountId, decimal Amount, DebitCredit Side);
    public record Command(String Description, DateTime Time, List<NewEntryDto> Entries) : IRequest<Result>;
    public record Result(Guid TransactionId);
}

