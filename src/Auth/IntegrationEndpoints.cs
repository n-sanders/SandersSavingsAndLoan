using Microsoft.EntityFrameworkCore;
using SandersSavingsAndLoan.Data;

namespace SandersSavingsAndLoan;

public static class IntegrationEndpoints
{
    public static void MapIntegrationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/integrations");

        group.MapPost("/tasks", async (CreateIntegrationTaskRequest req, HttpContext http, AppDbContext db) =>
        {
            var apiKey = await ApiKeyAuth.RequireApiKeyAsync(http, db);
            if (apiKey is null)
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Description))
                return Results.BadRequest(new { error = "Description is required." });

            if (req.SuggestedAmountCents <= 0)
                return Results.BadRequest(new { error = "Suggested amount must be positive." });

            var account = await ResolveKidAccountAsync(db, req.AccountId, req.Username);
            if (account.error is not null)
                return account.error;

            var externalId = string.IsNullOrWhiteSpace(req.ExternalId) ? null : req.ExternalId.Trim();
            if (externalId is not null)
            {
                if (externalId.Length > 128)
                    return Results.BadRequest(new { error = "External id is too long." });

                var existing = await db.TaskSubmissions
                    .FirstOrDefaultAsync(t => t.Source == apiKey.Source && t.ExternalId == externalId);

                if (existing is not null)
                {
                    return Results.Ok(ToTaskResponse(existing));
                }
            }

            var task = new TaskSubmission
            {
                AccountId = account.account!.Id,
                Description = req.Description.Trim(),
                SuggestedAmountCents = req.SuggestedAmountCents,
                Status = TaskStatuses.Pending,
                Source = apiKey.Source,
                ExternalId = externalId,
                CreatedAt = DateTime.UtcNow,
            };

            db.TaskSubmissions.Add(task);
            await db.SaveChangesAsync();

            return Results.Created($"/api/integrations/tasks/{task.Id}", ToTaskResponse(task));
        });
    }

    private static async Task<(Account? account, IResult? error)> ResolveKidAccountAsync(
        AppDbContext db,
        int? accountId,
        string? username)
    {
        var hasAccountId = accountId is > 0;
        var hasUsername = !string.IsNullOrWhiteSpace(username);

        if (!hasAccountId && !hasUsername)
            return (null, Results.BadRequest(new { error = "Provide username or accountId." }));

        Account? byId = null;
        Account? byUsername = null;

        if (hasAccountId)
        {
            byId = await db.Accounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == accountId);

            if (byId is null || byId.User.Role != Roles.Kid)
                return (null, Results.NotFound(new { error = "Account not found." }));
        }

        if (hasUsername)
        {
            var normalized = username!.Trim().ToLowerInvariant();
            var user = await db.Users
                .Include(u => u.Account)
                .FirstOrDefaultAsync(u => u.Username == normalized && u.Role == Roles.Kid);

            if (user?.Account is null)
                return (null, Results.NotFound(new { error = "Account not found." }));

            byUsername = user.Account;
        }

        if (byId is not null && byUsername is not null && byId.Id != byUsername.Id)
            return (null, Results.BadRequest(new { error = "username and accountId do not match." }));

        return (byId ?? byUsername, null);
    }

    private static object ToTaskResponse(TaskSubmission task) => new
    {
        task.Id,
        accountId = task.AccountId,
        task.Description,
        task.SuggestedAmountCents,
        task.Status,
        source = task.Source,
        externalId = task.ExternalId,
        task.CreatedAt,
    };
}

public record CreateIntegrationTaskRequest(
    string Description,
    int SuggestedAmountCents,
    string? Username,
    int? AccountId,
    string? ExternalId);
