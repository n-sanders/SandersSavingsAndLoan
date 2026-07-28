using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SandersSavingsAndLoan.Data;

namespace SandersSavingsAndLoan;

public static class KidEndpoints
{
    public static void MapKidEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/me")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Kid));

        group.MapGet("/account", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = await AuthEndpoints.GetCurrentUserAsync(principal, db);
            if (user?.Account is null)
                return Results.NotFound(new { error = "Account not found." });

            return Results.Ok(new
            {
                accountId = user.Account.Id,
                displayName = user.DisplayName,
                balanceCents = user.Account.BalanceCents,
            });
        });

        group.MapGet("/transactions", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = await AuthEndpoints.GetCurrentUserAsync(principal, db);
            if (user?.Account is null)
                return Results.NotFound(new { error = "Account not found." });

            var txs = await db.Transactions
                .Where(t => t.AccountId == user.Account.Id)
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

        group.MapGet("/tasks", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = await AuthEndpoints.GetCurrentUserAsync(principal, db);
            if (user?.Account is null)
                return Results.NotFound(new { error = "Account not found." });

            var tasks = await db.TaskSubmissions
                .Where(t => t.AccountId == user.Account.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id,
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

        group.MapPost("/tasks", async (CreateTaskRequest req, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = await AuthEndpoints.GetCurrentUserAsync(principal, db);
            if (user?.Account is null)
                return Results.NotFound(new { error = "Account not found." });

            if (string.IsNullOrWhiteSpace(req.Description))
                return Results.BadRequest(new { error = "Description is required." });

            if (req.SuggestedAmountCents <= 0)
                return Results.BadRequest(new { error = "Suggested amount must be positive." });

            var task = new TaskSubmission
            {
                AccountId = user.Account.Id,
                Description = req.Description.Trim(),
                SuggestedAmountCents = req.SuggestedAmountCents,
                Status = TaskStatuses.Pending,
                CreatedAt = DateTime.UtcNow,
            };

            db.TaskSubmissions.Add(task);
            await db.SaveChangesAsync();

            return Results.Created($"/api/me/tasks/{task.Id}", new
            {
                task.Id,
                task.Description,
                task.SuggestedAmountCents,
                task.Status,
                task.CreatedAt,
            });
        });
    }
}

public record CreateTaskRequest(string Description, int SuggestedAmountCents);
