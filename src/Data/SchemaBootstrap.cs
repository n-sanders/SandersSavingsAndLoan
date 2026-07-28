using Microsoft.EntityFrameworkCore;

namespace SandersSavingsAndLoan.Data;

/// <summary>
/// EnsureCreated only creates a brand-new database. For existing ssl.db files,
/// add tables / columns with IF NOT EXISTS / conditional ALTER.
/// </summary>
public static class SchemaBootstrap
{
    public static async Task EnsureLoanSchemaAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "LoanRequests" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_LoanRequests" PRIMARY KEY AUTOINCREMENT,
                "AccountId" INTEGER NOT NULL,
                "AmountCents" INTEGER NOT NULL,
                "Purpose" TEXT NOT NULL,
                "TermWeeks" INTEGER NOT NULL,
                "WeeklyPaymentCents" INTEGER NOT NULL,
                "TotalRepayCents" INTEGER NOT NULL,
                "TotalInterestCents" INTEGER NOT NULL,
                "Status" TEXT NOT NULL,
                "BankerNote" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "ReviewedAt" TEXT NULL,
                "ReviewedByUserId" INTEGER NULL,
                CONSTRAINT "FK_LoanRequests_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_LoanRequests_Users_ReviewedByUserId" FOREIGN KEY ("ReviewedByUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "LoanInstallments" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_LoanInstallments" PRIMARY KEY AUTOINCREMENT,
                "LoanRequestId" INTEGER NOT NULL,
                "Sequence" INTEGER NOT NULL,
                "DueDate" TEXT NOT NULL,
                "AmountCents" INTEGER NOT NULL,
                "InterestCents" INTEGER NOT NULL,
                "PrincipalCents" INTEGER NOT NULL,
                "Status" TEXT NOT NULL,
                "PaidAt" TEXT NULL,
                CONSTRAINT "FK_LoanInstallments_LoanRequests_LoanRequestId" FOREIGN KEY ("LoanRequestId") REFERENCES "LoanRequests" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_LoanInstallments_Status_DueDate"
            ON "LoanInstallments" ("Status", "DueDate");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_LoanInstallments_LoanRequestId"
            ON "LoanInstallments" ("LoanRequestId");
            """);

        // Existing databases created before loans won't have this column.
        var hasLoanInstallmentId = await ColumnExistsAsync(db, "Transactions", "LoanInstallmentId");
        if (!hasLoanInstallmentId)
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Transactions" ADD COLUMN "LoanInstallmentId" INTEGER NULL;
                """);
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Transactions_LoanInstallmentId"
            ON "Transactions" ("LoanInstallmentId")
            WHERE "LoanInstallmentId" IS NOT NULL;
            """);
    }

    public static async Task EnsureTaskIntegrationSchemaAsync(AppDbContext db)
    {
        var hasSource = await ColumnExistsAsync(db, "TaskSubmissions", "Source");
        if (!hasSource)
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "TaskSubmissions" ADD COLUMN "Source" TEXT NULL;
                """);
        }

        var hasExternalId = await ColumnExistsAsync(db, "TaskSubmissions", "ExternalId");
        if (!hasExternalId)
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "TaskSubmissions" ADD COLUMN "ExternalId" TEXT NULL;
                """);
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TaskSubmissions_Source_ExternalId"
            ON "TaskSubmissions" ("Source", "ExternalId")
            WHERE "ExternalId" IS NOT NULL;
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "IntegrationApiKeys" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_IntegrationApiKeys" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Source" TEXT NOT NULL,
                "KeyPrefix" TEXT NOT NULL,
                "KeyHash" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "CreatedByUserId" INTEGER NOT NULL,
                "RevokedAt" TEXT NULL,
                CONSTRAINT "FK_IntegrationApiKeys_Users_CreatedByUserId"
                    FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_IntegrationApiKeys_Source"
            ON "IntegrationApiKeys" ("Source")
            WHERE "RevokedAt" IS NULL;
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_IntegrationApiKeys_KeyHash"
            ON "IntegrationApiKeys" ("KeyHash");
            """);
    }

    private static async Task<bool> ColumnExistsAsync(AppDbContext db, string table, string column)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
