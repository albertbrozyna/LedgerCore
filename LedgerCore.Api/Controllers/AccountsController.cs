using LedgerCore.Api.Dtos;
using LedgerCore.Domain.Entities;
using LedgerCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LedgerCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly LedgerDbContext _context;

    // Wstrzykujemy bazę danych (Dependency Injection)
    public AccountsController(LedgerDbContext context)
    {
        _context = context;
    }

    // POST: api/accounts
    [HttpPost]
    public IActionResult Create(CreateAccountRequest request)
    {
        // 1. Tworzymy prawdziwe Konto na podstawie danych z żądania
        // Używamy naszego bezpiecznego konstruktora z Domain!
        var newAccount = new Account(request.Name, request.Code, request.Type);

        // 2. Dodajemy do bazy (na razie tylko w pamięci EF)
        _context.Accounts.Add(newAccount);

        // 3. Zapisujemy zmiany (INSERT INTO...) - tu leci SQL do bazy
        _context.SaveChanges();

        // 4. Zwracamy ID nowo utworzonego konta
        return Ok(newAccount.Id);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {   
        var accounts = await _context.Accounts.ToListAsync();

        return Ok(accounts);
    }


    // GET: api/accounts/{id}
    // Np. api/accounts/3fa85f64-5717-4562-b3fc-2c963f66afa6
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        // Szukamy konta w bazie po ID
        var account = await _context.Accounts.FindAsync(id);
        if (account == null)
        {
            return NotFound("Nie znaleziono konta o id" + id); // 404 jeśli nie znaleziono
        }
        return Ok(account); // 200 + dane konta jeśli znaleziono
    }


    [HttpGet("{id}/balance")]
    public async Task<IActionResult> GetBalance(Guid id)
    {
        // Sprawdzamy, czy konto istnieje
        var account = await _context.Accounts.FindAsync(id);
        if (account == null)
        {
            return NotFound("Nie znaleziono konta o id " + id); // 404 jeśli nie znaleziono
        }
        // Obliczamy saldo konta
        var debitSum = await _context.JournalEntries
            .Where(e => e.AccountId == id && e.Side == Domain.Enums.DebitCredit.Debit)
            .SumAsync(e => e.Amount);
        var creditSum = await _context.JournalEntries
            .Where(e => e.AccountId == id && e.Side == Domain.Enums.DebitCredit.Credit)
            .SumAsync(e => e.Amount);
        var balance = debitSum - creditSum;

        var result = new AccountBalanceDto
        {
            AccountName = account.Name,
            Balance = balance,
            AccountCode = account.Code,
            TotalCredit = creditSum,
            TotalDebit = debitSum
        };

        return Ok(result);
    }


    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        // Sprawdzamy, czy konto istnieje
        var account = await _context.Accounts.FindAsync(id);
        if (account == null)
        {
            return NotFound("Nie znaleziono konta o id " + id); // 404 jeśli nie znaleziono
        }
        // Pobieramy historię zapisów księgowych dla tego konta
        var entries = await _context.JournalEntries
            .Include(e => e.Transaction)
            .Where(e => e.AccountId == id)
            .OrderByDescending(e => e.Transaction.Date)
            .Select(
            
                e => new AccountHistoryDto
                {
                    Date = e.Transaction.Date,
                    Description = e.Transaction.Description,
                    Amount = e.Amount,
                    Side = e.Side == Domain.Enums.DebitCredit.Debit ? "Winien" : "Ma"
                }
            )
            .ToListAsync();
        return Ok(entries);
    }


}