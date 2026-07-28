using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SandersSavingsAndLoan.Data;

namespace SandersSavingsAndLoan;

public static class BankerEndpoints
{
    public static void MapBankerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/banker")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Banker));

        group.MapGet("/accounts", async (AppDbContext db) =>
        {
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

            if (account.BalanceCents < req.AmountCents)
                return Results.BadRequest(new { error = "Insufficient balance." });

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
    }
}

public record MoneyMovementRequest(int AccountId, int AmountCents, string? Note);
public record ReviewTaskRequest(int FinalAmountCents, string? Note);
public record RejectTaskRequest(string? Note);
