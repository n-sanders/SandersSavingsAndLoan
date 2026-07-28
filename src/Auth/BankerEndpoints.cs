using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SandersSavingsAndLoan.Data;
using SandersSavingsAndLoan.Loans;

namespace SandersSavingsAndLoan;

public static class BankerEndpoints
{
    public static void MapBankerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/banker")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Banker));

        group.MapGet("/accounts", async (AppDbContext db) =>
        {
            await LoanPaymentProcessor.ProcessDueAsync(db);

            var accounts = await db.Accounts
                .Include(a => a.User)
                .OrderBy(a => a.User.DisplayName)
                .Select(a => new
                {
                    a.Id,
                    userId = a.UserId,
                    displayName = a.User.DisplayName,
                    username = a.User.Username,
                    balanceCents = a.BalanceCents,
                })
                .ToListAsync();

            return Results.Ok(accounts);
        });

        group.MapGet("/accounts/{id:int}/transactions", async (int id, AppDbContext db) =>
        {
            await LoanPaymentProcessor.ProcessDueAsync(db);

            var exists = await db.Accounts.AnyAsync(a => a.Id == id);
            if (!exists)
                return Results.NotFound(new { error = "Account not found." });

            var txs = await db.Transactions
                .Where(t => t.AccountId == id)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id,
                    t.Type,
                    t.AmountCents,
                    t.Note,
                    t.TaskSubmissionId,
                    t.LoanInstallmentId,
                    t.CreatedAt,
                })
                .ToListAsync();

            return Results.Ok(txs);
        });

        group.MapPost("/deposits", async (MoneyMovementRequest req, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (req.AmountCents <= 0)
                return Results.BadRequest(new { error = "Amount must be positive." });

            var banker = await AuthEndpoints.GetCurrentUserAsync(principal, db);
            if (banker is null)
                return Results.Unauthorized();

            var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == req.AccountId);
            if (account is null)
                return Results.NotFound(new { error = "Account not found." });

            await using var tx = await db.Database.BeginTransactionAsync();

            account.BalanceCents += req.AmountCents;
            var entry = new Transaction
            {
                AccountId = account.Id,
                Type = TransactionTypes.Deposit,
                AmountCents = req.AmountCents,
                Note = string.IsNullOrWhiteSpace(req.Note) ? "Deposit" : req.Note.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = banker.Id,
            };
            db.Transactions.Add(entry);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.Ok(new
            {
                entry.Id,
                entry.Type,
                entry.AmountCents,
                entry.Note,
                entry.CreatedAt,
                balanceCents = account.BalanceCents,
            });
        });

        group.MapPost("/withdrawals", async (MoneyMovementRequest req, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (req.AmountCents <= 0)
                return Results.BadRequest(new { error = "Amount must be positive." });

            var banker = await AuthEndpoints.GetCurrentUserAsync(principal, db);
            if (banker is null)
                return Results.Unauthorized();

            var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == req.AccountId);
            if (account is null)
                return Results.NotFound(new { error = "Account not found." });

            await using var tx = await db.Database.BeginTransactionAsync();

            account.BalanceCents -= req.AmountCents;
            var entry = new Transaction
            {
                AccountId = account.Id,
                Type = TransactionTypes.Withdrawal,
                AmountCents = req.AmountCents,
                Note = string.IsNullOrWhiteSpace(req.Note) ? "Withdrawal" : req.Note.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = banker.Id,
            };
            db.Transactions.Add(entry);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.Ok(new
            {
                entry.Id,
                entry.Type,
                entry.AmountCents,
                entry.Note,
                entry.CreatedAt,
                balanceCents = account.BalanceCents,
            });
        });

        group.MapGet("/tasks", async (AppDbContext db, string? status) =>
        {
            var filter = string.IsNullOrWhiteSpace(status) ? TaskStatuses.Pending : status.Trim();

            var tasks = await db.TaskSubmissions
                .Include(t => t.Account)
                .ThenInclude(a => a.User)
                .Where(t => t.Status == filter)
                .OrderBy(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id,
                    accountId = t.AccountId,
                    displayName = t.Account.User.DisplayName,
                    t.Description,
                    t.SuggestedAmountCents,
                    t.Status,
                    t.FinalAmountCents,
                    t.BankerNote,
                    t.CreatedAt,
                    t.ReviewedAt,
                })
                .ToListAsync();

            return Results.Ok(tasks);
        });

        group.MapPost("/tasks/{id:int}/approve", async (int id, ReviewTaskRequest req, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (req.FinalAmountCents <= 0)
                return Results.BadRequest(new { error = "Final amount must be positive." });

            var banker = await AuthEndpoints.GetCurrentUserAsync(principal, db);
            if (banker is null)
                return Results.Unauthorized();

            var task = await db.TaskSubmissions
                .Include(t => t.Account)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task is null)
                return Results.NotFound(new { error = "Task not found." });

            if (task.Status != TaskStatuses.Pending)
                return Results.BadRequest(new { error = "Task is not pending." });

            await using var tx = await db.Database.BeginTransactionAsync();

            var now = DateTime.UtcNow;
            task.Status = TaskStatuses.Approved;
            task.FinalAmountCents = req.FinalAmountCents;
            task.BankerNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
            task.ReviewedAt = now;
            task.ReviewedByUserId = banker.Id;

            task.Account.BalanceCents += req.FinalAmountCents;

            var entry = new Transaction
            {
                AccountId = task.AccountId,
                Type = TransactionTypes.Deposit,
                AmountCents = req.FinalAmountCents,
                Note = $"Task: {task.Description}",
                TaskSubmissionId = task.Id,
                CreatedAt = now,
                CreatedByUserId = banker.Id,
            };
            db.Transactions.Add(entry);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.Ok(new
            {
                task.Id,
                task.Status,
                task.FinalAmountCents,
                task.BankerNote,
                task.ReviewedAt,
                transactionId = entry.Id,
                balanceCents = task.Account.BalanceCents,
            });
        });

        group.MapPost("/tasks/{id:int}/reject", async (int id, RejectTaskRequest req, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var banker = await AuthEndpoints.GetCurrentUserAsync(principal, db);
            if (banker is null)
                return Results.Unauthorized();

            var task = await db.TaskSubmissions.FirstOrDefaultAsync(t => t.Id == id);
            if (task is null)
                return Results.NotFound(new { error = "Task not found." });

            if (task.Status != TaskStatuses.Pending)
                return Results.BadRequest(new { error = "Task is not pending." });

            task.Status = TaskStatuses.Rejected;
            task.BankerNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
            task.ReviewedAt = DateTime.UtcNow;
            task.ReviewedByUserId = banker.Id;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                task.Id,
                task.Status,
                task.BankerNote,
                task.ReviewedAt,
            });
        });

        group.MapGet("/loans", async (AppDbContext db, string? status) =>
        {
            await LoanPaymentProcessor.ProcessDueAsync(db);

            var filter = string.IsNullOrWhiteSpace(status) ? LoanStatuses.Pending : status.Trim();

            var loans = await db.LoanRequests
                .Include(l => l.Account)
                .ThenInclude(a => a.User)
                .Include(l => l.Installments)
                .Where(l => l.Status == filter)
                .OrderBy(l => l.CreatedAt)
                .Select(l => new
                {
                    l.Id,
                    accountId = l.AccountId,
                    displayName = l.Account.User.DisplayName,
                    l.AmountCents,
                    l.Purpose,
                    l.TermWeeks,
                    l.WeeklyPaymentCents,
                    l.TotalRepayCents,
                    l.TotalInterestCents,
                    l.Status,
                    l.BankerNote,
                    l.CreatedAt,
                    l.ReviewedAt,
                    installments = l.Installments
                        .OrderBy(i => i.Sequence)
                        .Select(i => new
                        {
                            i.Id,
                            i.Sequence,
                            dueDate = i.DueDate.ToString("yyyy-MM-dd"),
                            i.AmountCents,
                            i.InterestCents,
                            i.PrincipalCents,
                            i.Status,
                            i.PaidAt,
                        })
                        .ToList(),
                })
                .ToListAsync();

            return Results.Ok(loans);
        });

        group.MapPost("/loans/{id:int}/approve", async (int id, RejectTaskRequest req, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var banker = await AuthEndpoints.GetCurrentUserAsync(principal, db);
            if (banker is null)
                return Results.Unauthorized();

            var loan = await db.LoanRequests
                .Include(l => l.Account)
                .Include(l => l.Installments)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan is null)
                return Results.NotFound(new { error = "Loan not found." });

            if (loan.Status != LoanStatuses.Pending)
                return Results.BadRequest(new { error = "Loan is not pending." });

            var now = DateTime.UtcNow;
            var start = DateOnly.FromDateTime(now);
            var schedule = LoanCalculator.BuildSchedule(loan.AmountCents, loan.TermWeeks, start);

            await using var tx = await db.Database.BeginTransactionAsync();

            loan.Status = LoanStatuses.Approved;
            loan.BankerNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
            loan.ReviewedAt = now;
            loan.ReviewedByUserId = banker.Id;
            loan.WeeklyPaymentCents = schedule.WeeklyPaymentCents;
            loan.TotalRepayCents = schedule.TotalRepayCents;
            loan.TotalInterestCents = schedule.TotalInterestCents;

            loan.Account.BalanceCents += loan.AmountCents;

            var entry = new Transaction
            {
                AccountId = loan.AccountId,
                Type = TransactionTypes.Deposit,
                AmountCents = loan.AmountCents,
                Note = $"Loan: {loan.Purpose}",
                CreatedAt = now,
                CreatedByUserId = banker.Id,
            };
            db.Transactions.Add(entry);

            foreach (var row in schedule.Schedule)
            {
                db.LoanInstallments.Add(new LoanInstallment
                {
                    LoanRequestId = loan.Id,
                    Sequence = row.Sequence,
                    DueDate = row.DueDate,
                    AmountCents = row.AmountCents,
                    InterestCents = row.InterestCents,
                    PrincipalCents = row.PrincipalCents,
                    Status = LoanInstallmentStatuses.Scheduled,
                });
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.Ok(new
            {
                loan.Id,
                loan.Status,
                loan.BankerNote,
                loan.ReviewedAt,
                loan.WeeklyPaymentCents,
                loan.TotalRepayCents,
                loan.TotalInterestCents,
                transactionId = entry.Id,
                balanceCents = loan.Account.BalanceCents,
            });
        });

        group.MapPost("/loans/{id:int}/reject", async (int id, RejectTaskRequest req, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var banker = await AuthEndpoints.GetCurrentUserAsync(principal, db);
            if (banker is null)
                return Results.Unauthorized();

            var loan = await db.LoanRequests.FirstOrDefaultAsync(l => l.Id == id);
            if (loan is null)
                return Results.NotFound(new { error = "Loan not found." });

            if (loan.Status != LoanStatuses.Pending)
                return Results.BadRequest(new { error = "Loan is not pending." });

            loan.Status = LoanStatuses.Rejected;
            loan.BankerNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
            loan.ReviewedAt = DateTime.UtcNow;
            loan.ReviewedByUserId = banker.Id;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                loan.Id,
                loan.Status,
                loan.BankerNote,
                loan.ReviewedAt,
            });
        });

        group.MapPost("/kids/{userId:int}/passphrase", async (int userId, SetKidPassphraseRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Passphrase))
                return Results.BadRequest(new { error = "Passphrase is required." });

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
                return Results.NotFound(new { error = "User not found." });

            if (user.Role != Roles.Kid)
                return Results.BadRequest(new { error = "Only kid passphrases can be set here." });

            var hasher = new PasswordHasher<User>();
            user.PassphraseHash = hasher.HashPassword(user, req.Passphrase);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                ok = true,
                userId = user.Id,
                displayName = user.DisplayName,
                username = user.Username,
            });
        });
    }
}

public record MoneyMovementRequest(int AccountId, int AmountCents, string? Note);
public record ReviewTaskRequest(int FinalAmountCents, string? Note);
public record RejectTaskRequest(string? Note);
public record SetKidPassphraseRequest(string Passphrase);
