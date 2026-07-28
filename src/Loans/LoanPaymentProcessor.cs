using Microsoft.EntityFrameworkCore;
using SandersSavingsAndLoan.Data;

namespace SandersSavingsAndLoan.Loans;

public static class LoanPaymentProcessor
{
    /// <summary>
    /// Posts all scheduled installments that are due on or before today (UTC).
    /// Safe to call on every login / page-load API; idempotent for already-paid rows.
    /// </summary>
    public static async Task ProcessDueAsync(AppDbContext db, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var due = await db.LoanInstallments
            .Include(i => i.LoanRequest)
            .ThenInclude(l => l.Account)
            .Where(i => i.Status == LoanInstallmentStatuses.Scheduled && i.DueDate <= today)
            .OrderBy(i => i.DueDate)
            .ThenBy(i => i.LoanRequestId)
            .ThenBy(i => i.Sequence)
            .ToListAsync(ct);

        if (due.Count == 0)
            return;

        // System actor for automated withdrawals: prefer banker user, else first user.
        var systemUserId = await db.Users
            .Where(u => u.Role == Roles.Banker)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct)
            ?? await db.Users.Select(u => u.Id).FirstAsync(ct);

        var now = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        foreach (var installment in due)
        {
            var account = installment.LoanRequest.Account;
            account.BalanceCents -= installment.AmountCents;

            var entry = new Transaction
            {
                AccountId = account.Id,
                Type = TransactionTypes.Withdrawal,
                AmountCents = installment.AmountCents,
                Note = $"Loan payment #{installment.Sequence}: {installment.LoanRequest.Purpose}",
                LoanInstallmentId = installment.Id,
                CreatedAt = now,
                CreatedByUserId = systemUserId,
            };
            db.Transactions.Add(entry);

            installment.Status = LoanInstallmentStatuses.Paid;
            installment.PaidAt = now;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
