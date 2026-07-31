using Microsoft.EntityFrameworkCore;
using SandersSavingsAndLoan.Data;

namespace SandersSavingsAndLoan.Interest;

public record InterestAccountPreview(
    int AccountId,
    string DisplayName,
    int AverageDailyBalanceCents,
    int InterestCents);

public record InterestPreviewResult(
    bool Pending,
    int? AccrualYear,
    int? AccrualMonth,
    string? AccrualMonthLabel,
    DateOnly? PayoutDate,
    IReadOnlyList<InterestAccountPreview> Accounts,
    int TotalInterestCents);

public static class SavingsInterestProcessor
{
    public static async Task<DateOnly?> GetNextAccrualMonthAsync(AppDbContext db, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var latest = await db.InterestPaymentRuns
            .OrderByDescending(r => r.AccrualYear)
            .ThenByDescending(r => r.AccrualMonth)
            .Select(r => new { r.AccrualYear, r.AccrualMonth })
            .FirstOrDefaultAsync(ct);

        DateOnly candidate;
        if (latest is not null)
        {
            candidate = new DateOnly(latest.AccrualYear, latest.AccrualMonth, 1).AddMonths(1);
        }
        else
        {
            if (!await db.Accounts.AnyAsync(ct))
                return null;

            var earliestCreated = await db.Accounts
                .MinAsync(a => a.CreatedAt, ct);

            var start = DateOnly.FromDateTime(earliestCreated);
            candidate = new DateOnly(start.Year, start.Month, 1);
        }

        var payoutDate = candidate.AddMonths(1);
        if (payoutDate > today)
            return null;

        return candidate;
    }

    public static async Task<InterestPreviewResult> PreviewAsync(AppDbContext db, CancellationToken ct = default)
    {
        var accrual = await GetNextAccrualMonthAsync(db, ct);
        if (accrual is null)
        {
            return new InterestPreviewResult(
                Pending: false,
                AccrualYear: null,
                AccrualMonth: null,
                AccrualMonthLabel: null,
                PayoutDate: null,
                Accounts: [],
                TotalInterestCents: 0);
        }

        var monthStart = accrual.Value;
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var accounts = await BuildAccountPreviewsAsync(db, monthStart, monthEnd, ct);

        return new InterestPreviewResult(
            Pending: true,
            AccrualYear: monthStart.Year,
            AccrualMonth: monthStart.Month,
            AccrualMonthLabel: monthStart.ToString("MMMM yyyy"),
            PayoutDate: monthStart.AddMonths(1),
            Accounts: accounts,
            TotalInterestCents: accounts.Sum(a => a.InterestCents));
    }

    public static async Task<(InterestPreviewResult? Paid, string? Error)> PayAsync(
        AppDbContext db,
        int bankerUserId,
        CancellationToken ct = default)
    {
        var preview = await PreviewAsync(db, ct);
        if (!preview.Pending || preview.AccrualYear is null || preview.AccrualMonth is null || preview.PayoutDate is null)
            return (null, "No interest period is due yet.");

        var alreadyPaid = await db.InterestPaymentRuns.AnyAsync(
            r => r.AccrualYear == preview.AccrualYear && r.AccrualMonth == preview.AccrualMonth,
            ct);
        if (alreadyPaid)
            return (null, "Interest for that month was already paid.");

        var payoutAt = preview.PayoutDate.Value.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        var paidAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var run = new InterestPaymentRun
        {
            AccrualYear = preview.AccrualYear.Value,
            AccrualMonth = preview.AccrualMonth.Value,
            PaidAt = paidAt,
            PaidByUserId = bankerUserId,
        };
        db.InterestPaymentRuns.Add(run);
        await db.SaveChangesAsync(ct);

        var accountIds = preview.Accounts.Select(a => a.AccountId).ToList();
        var accounts = await db.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        foreach (var row in preview.Accounts.Where(a => a.InterestCents > 0))
        {
            if (!accounts.TryGetValue(row.AccountId, out var account))
                continue;

            account.BalanceCents += row.InterestCents;
            db.Transactions.Add(new Transaction
            {
                AccountId = account.Id,
                Type = TransactionTypes.Deposit,
                AmountCents = row.InterestCents,
                Note = SavingsInterestCalculator.FormatInterestNote(row.AverageDailyBalanceCents, row.InterestCents),
                InterestPaymentRunId = run.Id,
                CreatedAt = payoutAt,
                CreatedByUserId = bankerUserId,
            });
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return (preview, null);
    }

    private static async Task<IReadOnlyList<InterestAccountPreview>> BuildAccountPreviewsAsync(
        AppDbContext db,
        DateOnly monthStart,
        DateOnly monthEnd,
        CancellationToken ct)
    {
        var accounts = await db.Accounts
            .Include(a => a.User)
            .Include(a => a.Transactions)
            .OrderBy(a => a.User.DisplayName)
            .ToListAsync(ct);

        return accounts.Select(a =>
        {
            var adb = SavingsInterestCalculator.ComputeAverageDailyBalanceCents(a.Transactions, monthStart, monthEnd);
            var interest = SavingsInterestCalculator.ComputeInterestCents(adb);
            return new InterestAccountPreview(a.Id, a.User.DisplayName, adb, interest);
        }).ToList();
    }
}
