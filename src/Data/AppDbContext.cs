using Microsoft.EntityFrameworkCore;

namespace SandersSavingsAndLoan.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<TaskSubmission> TaskSubmissions => Set<TaskSubmission>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<LoanRequest> LoanRequests => Set<LoanRequest>();
    public DbSet<LoanInstallment> LoanInstallments => Set<LoanInstallment>();
    public DbSet<IntegrationApiKey> IntegrationApiKeys => Set<IntegrationApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).HasMaxLength(64).IsRequired();
            e.Property(u => u.DisplayName).HasMaxLength(128).IsRequired();
            e.Property(u => u.Role).HasMaxLength(32).IsRequired();
            e.Property(u => u.PassphraseHash).IsRequired();
        });

        modelBuilder.Entity<Account>(e =>
        {
            e.HasIndex(a => a.UserId).IsUnique();
            e.HasOne(a => a.User)
                .WithOne(u => u.Account)
                .HasForeignKey<Account>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskSubmission>(e =>
        {
            e.Property(t => t.Description).HasMaxLength(500).IsRequired();
            e.Property(t => t.Status).HasMaxLength(32).IsRequired();
            e.Property(t => t.Source).HasMaxLength(64);
            e.Property(t => t.ExternalId).HasMaxLength(128);
            e.HasIndex(t => new { t.Source, t.ExternalId })
                .IsUnique()
                .HasFilter("\"ExternalId\" IS NOT NULL");
            e.HasOne(t => t.Account)
                .WithMany(a => a.TaskSubmissions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.ReviewedByUser)
                .WithMany()
                .HasForeignKey(t => t.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<IntegrationApiKey>(e =>
        {
            e.Property(k => k.Name).HasMaxLength(128).IsRequired();
            e.Property(k => k.Source).HasMaxLength(64).IsRequired();
            e.Property(k => k.KeyPrefix).HasMaxLength(16).IsRequired();
            e.Property(k => k.KeyHash).HasMaxLength(128).IsRequired();
            e.HasIndex(k => k.Source)
                .IsUnique()
                .HasFilter("\"RevokedAt\" IS NULL");
            e.HasIndex(k => k.KeyHash).IsUnique();
            e.HasOne(k => k.CreatedByUser)
                .WithMany()
                .HasForeignKey(k => k.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoanRequest>(e =>
        {
            e.Property(l => l.Purpose).HasMaxLength(500).IsRequired();
            e.Property(l => l.Status).HasMaxLength(32).IsRequired();
            e.HasOne(l => l.Account)
                .WithMany(a => a.LoanRequests)
                .HasForeignKey(l => l.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.ReviewedByUser)
                .WithMany()
                .HasForeignKey(l => l.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LoanInstallment>(e =>
        {
            e.Property(i => i.Status).HasMaxLength(32).IsRequired();
            e.HasIndex(i => new { i.Status, i.DueDate });
            e.HasOne(i => i.LoanRequest)
                .WithMany(l => l.Installments)
                .HasForeignKey(i => i.LoanRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.Property(t => t.Type).HasMaxLength(32).IsRequired();
            e.Property(t => t.Note).HasMaxLength(500).IsRequired();
            e.HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.TaskSubmission)
                .WithOne(ts => ts.Transaction)
                .HasForeignKey<Transaction>(t => t.TaskSubmissionId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.LoanInstallment)
                .WithOne(i => i.Transaction)
                .HasForeignKey<Transaction>(t => t.LoanInstallmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
