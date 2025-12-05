// 1. Główna koperta z danymi transakcji
using LedgerCore.Api.Dtos;
using LedgerCore.Application.Common.Interfaces;
using MediatR;
using System.ComponentModel.DataAnnotations;


public static class CreateTransaction
{ 
    public record Command(String description, decimal Amount) : IRequest<Result>;
    public record Result(Guid TransactionId,String descriptio,decimal Amount);

    public class Handler : IRequestHandler<Command, Result>
    {
        IAppDbContext _context;

        public Handler(IAppDbContext context)
        {
            _context = context;
        }


        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            // Logika tworzenia transakcji
            Guid newTransactionId = Guid.NewGuid();





        }

public class CreateTransactionCommand
{
    [Required(ErrorMessage = "Opis transakcji jest wymagany")]
    public string Description { get; set; }

    public DateTime Date { get; set; }

    // Lista wpisów (musi być co najmniej 2, żeby bilans miał sens)
    [Required]
    [MinLength(2, ErrorMessage = "Transakcja musi mieć co najmniej 2 zapisy!")]
    public List<TransactionEntryDto> Entries { get; set; }
}

