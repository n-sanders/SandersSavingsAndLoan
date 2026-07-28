using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SandersSavingsAndLoan.Data;

public static class DbSeeder
{
    private static readonly (string Username, string DisplayName, string Role)[] SeedUsers =
    [
        ("banker", "Banker", Roles.Banker),
        ("evie", "Evie", Roles.Kid),
        ("noah", "Noah", Roles.Kid),
        ("hannah", "Hannah", Roles.Kid),
        ("judah", "Judah", Roles.Kid),
        ("ezra", "Ezra", Roles.Kid),
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync())
            return;

        var hasher = new PasswordHasher<User>();
        var now = DateTime.UtcNow;

        foreach (var (username, displayName, role) in SeedUsers)
        {
            var user = new User
            {
                Username = username,
                DisplayName = displayName,
                Role = role,
                CreatedAt = now,
            };
            user.PassphraseHash = hasher.HashPassword(user, username);

            db.Users.Add(user);

            if (role == Roles.Kid)
            {
                db.Accounts.Add(new Account
                {
                    User = user,
                    BalanceCents = 0,
                    CreatedAt = now,
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
