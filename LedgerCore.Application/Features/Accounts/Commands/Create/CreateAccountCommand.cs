using LedgerCore.Domain.Enums;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace LedgerCore.Api.Dtos;

public static partial class CreateAccount
{

    public record Command(string Name,string Code, AccountType Type) : IRequest<Result>;
    public record Result(Guid AccountId);
}