using LedgerCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Features.Transactions.Common.Dtos;

public record JournalEntryDto(Guid Id, Guid TransactionId, Guid AccountId, decimal Amount, DebitCredit Side);
