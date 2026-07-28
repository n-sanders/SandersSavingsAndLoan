using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SandersSavingsAndLoan.Data;

namespace SandersSavingsAndLoan;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest req, AppDbContext db, HttpContext http) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Passphrase))
                return Results.BadRequest(new { error = "Username and passphrase are required." });

            var username = req.Username.Trim().ToLowerInvariant();
            var user = await db.Users
                .Include(u => u.Account)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user is null)
                return Results.Unauthorized();

            var hasher = new PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(user, user.PassphraseHash, req.Passphrase);
            if (result == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.GivenName, user.DisplayName),
                new(ClaimTypes.Role, user.Role),
            };
            if (user.Account is not null)
                claims.Add(new Claim("accountId", user.Account.Id.ToString()));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return Results.Ok(ToMe(user));
        });

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new { ok = true });
        }).RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = await GetCurrentUserAsync(principal, db);
            if (user is null)
                return Results.Unauthorized();
            return Results.Ok(ToMe(user));
        }).RequireAuthorization();
    }

    public static async Task<User?> GetCurrentUserAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (idValue is null || !int.TryParse(idValue, out var id))
            return null;

        return await db.Users
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public static object ToMe(User user) => new
    {
        id = user.Id,
        username = user.Username,
        displayName = user.DisplayName,
        role = user.Role,
        accountId = user.Account?.Id,
    };
}

public record LoginRequest(string Username, string Passphrase);
