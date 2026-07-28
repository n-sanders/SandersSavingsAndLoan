using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SandersSavingsAndLoan.Data;

public static class DbSeeder
{
    private const int SeedDepositCents = 500;

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

        var banker = CreateUser(hasher, "banker", "Banker", Roles.Banker, now);
        db.Users.Add(banker);
        await db.SaveChangesAsync();

        foreach (var (username, displayName, _) in SeedUsers.Where(u => u.Role == Roles.Kid))
        {
            var user = CreateUser(hasher, username, displayName, Roles.Kid, now);
            db.Users.Add(user);

            var account = new Account
            {
                User = user,
                BalanceCents = SeedDepositCents,
                CreatedAt = now,
            };
            db.Accounts.Add(account);

            db.Transactions.Add(new Transaction
            {
                Account = account,
                Type = TransactionTypes.Deposit,
                AmountCents = SeedDepositCents,
                Note = "Initial deposit",
                CreatedAt = now,
                CreatedByUserId = banker.Id,
            });
        }

        await db.SaveChangesAsync();
    }

    private static User CreateUser(
        PasswordHasher<User> hasher,
        string username,
        string displayName,
        string role,
        DateTime createdAt)
    {
        var user = new User
        {
            Username = username,
            DisplayName = displayName,
            Role = role,
            CreatedAt = createdAt,
        };
        user.PassphraseHash = hasher.HashPassword(user, username);
        return user;
    }
}
