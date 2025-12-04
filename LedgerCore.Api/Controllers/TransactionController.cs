using LedgerCore.Domain.Entities;
using LedgerCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LedgerCore.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class TransactionsController :ControllerBase
{

    private readonly LedgerDbContext _context;
    public TransactionsController(LedgerDbContext context)
    {
        _context = context;

    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTransactionRequest request)
    {
        var transaction = new Transaction(request.Description, request.Date);

        // 2. Dodajemy wpisy (Wiersze)
        foreach (var entryDto in request.Entries)
        {
            var account = await _context.Accounts.FindAsync(entryDto.AccountId);

            if (account == null)
            {
                return BadRequest($"Nie znaleziono konta o ID: {entryDto.AccountId}");
            }

            // Dodajemy wpis (Logika Domain sprawdzi, czy kwota > 0)
            transaction.AddEntry(account, entryDto.Amount, entryDto.Side);
        }

        try
        {
            transaction.Validate();
        }
        catch (InvalidOperationException ex)
        {
            // Jeśli bilans się nie zgadza, zwracamy błąd 400
            return BadRequest(ex.Message);
        }

        // 4. Jeśli wszystko OK -> Zapisujemy do bazy
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return Ok(transaction.Id);
    }

}

