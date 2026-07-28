using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SandersSavingsAndLoan.Data;
using SandersSavingsAndLoan.Loans;

namespace SandersSavingsAndLoan;

public static class KidEndpoints
{
    public static void MapKidEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/me")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Kid));

        group.MapGet("/account", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            await LoanPaymentProcessor.ProcessDueAsync(db);

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
            await LoanPaymentProcessor.ProcessDueAsync(db);

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
                    t.LoanInstallmentId,
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

        group.MapPost("/loans/preview", (LoanPreviewRequest req) =>
        {
            if (req.AmountCents <= 0)
                return Results.BadRequest(new { error = "Amount must be positive." });
            if (!LoanCalculator.IsAllowedTerm(req.TermWeeks))
                return Results.BadRequest(new { error = "Choose a term of 2, 3, 4, 6, 8, or 10 weeks." });

            var start = DateOnly.FromDateTime(DateTime.UtcNow);
            var schedule = LoanCalculator.BuildSchedule(req.AmountCents, req.TermWeeks, start);
            return Results.Ok(ToPreviewDto(schedule, req.AmountCents, req.TermWeeks));
        });

        group.MapGet("/loans", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            await LoanPaymentProcessor.ProcessDueAsync(db);

            var user = await AuthEndpoints.GetCurrentUserAsync(principal, db);
            if (user?.Account is null)
                return Results.NotFound(new { error = "Account not found." });

            var loans = await db.LoanRequests
                .Where(l => l.AccountId == user.Account.Id)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new
                {
                    l.Id,
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

        group.MapPost("/loans", async (CreateLoanRequest req, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = await AuthEndpoints.GetCurrentUserAsync(principal, db);
            if (user?.Account is null)
                return Results.NotFound(new { error = "Account not found." });

            if (string.IsNullOrWhiteSpace(req.Purpose))
                return Results.BadRequest(new { error = "Purpose is required." });
            if (req.AmountCents <= 0)
                return Results.BadRequest(new { error = "Amount must be positive." });
            if (!LoanCalculator.IsAllowedTerm(req.TermWeeks))
                return Results.BadRequest(new { error = "Choose a term of 2, 3, 4, 6, 8, or 10 weeks." });

            var start = DateOnly.FromDateTime(DateTime.UtcNow);
            var schedule = LoanCalculator.BuildSchedule(req.AmountCents, req.TermWeeks, start);

            var loan = new LoanRequest
            {
                AccountId = user.Account.Id,
                AmountCents = req.AmountCents,
                Purpose = req.Purpose.Trim(),
                TermWeeks = req.TermWeeks,
                WeeklyPaymentCents = schedule.WeeklyPaymentCents,
                TotalRepayCents = schedule.TotalRepayCents,
                TotalInterestCents = schedule.TotalInterestCents,
                Status = LoanStatuses.Pending,
                CreatedAt = DateTime.UtcNow,
            };

            db.LoanRequests.Add(loan);
            await db.SaveChangesAsync();

            return Results.Created($"/api/me/loans/{loan.Id}", new
            {
                loan.Id,
                loan.AmountCents,
                loan.Purpose,
                loan.TermWeeks,
                loan.WeeklyPaymentCents,
                loan.TotalRepayCents,
                loan.TotalInterestCents,
                loan.Status,
                loan.CreatedAt,
            });
        });
    }

    public static object ToPreviewDto(LoanScheduleResult schedule, int amountCents, int termWeeks) => new
    {
        amountCents,
        termWeeks,
        weeklyRate = LoanCalculator.WeeklyRate,
        weeklyPaymentCents = schedule.WeeklyPaymentCents,
        totalRepayCents = schedule.TotalRepayCents,
        totalInterestCents = schedule.TotalInterestCents,
        schedule = schedule.Schedule.Select(r => new
        {
            r.Sequence,
            dueDate = r.DueDate.ToString("yyyy-MM-dd"),
            r.AmountCents,
            r.InterestCents,
            r.PrincipalCents,
        }),
    };
}

public record CreateTaskRequest(string Description, int SuggestedAmountCents);
public record LoanPreviewRequest(int AmountCents, int TermWeeks);
public record CreateLoanRequest(string Purpose, int AmountCents, int TermWeeks);
