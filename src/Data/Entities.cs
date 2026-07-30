namespace SandersSavingsAndLoan.Data;

public static class Roles
{
    public const string Banker = "Banker";
    public const string Kid = "Kid";
}

public static class TaskStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class LoanStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

public static class LoanInstallmentStatuses
{
    public const string Scheduled = "Scheduled";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
}

public static class TransactionTypes
{
    public const string Deposit = "Deposit";
    public const string Withdrawal = "Withdrawal";
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public string PassphraseHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public Account? Account { get; set; }
}

public class Account
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BalanceCents { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public List<TaskSubmission> TaskSubmissions { get; set; } = [];
    public List<Transaction> Transactions { get; set; } = [];
    public List<LoanRequest> LoanRequests { get; set; } = [];
}

public class TaskSubmission
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Description { get; set; } = "";
    public int SuggestedAmountCents { get; set; }
    public string Status { get; set; } = TaskStatuses.Pending;
    public int? FinalAmountCents { get; set; }
    public string? BankerNote { get; set; }
    public string? Source { get; set; }
    public string? ExternalId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }

    public Account Account { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
    public Transaction? Transaction { get; set; }
}

public class IntegrationApiKey
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string KeyPrefix { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? RevokedAt { get; set; }

    public User CreatedByUser { get; set; } = null!;
}

public class LoanRequest
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int AmountCents { get; set; }
    public string Purpose { get; set; } = "";
    public int TermWeeks { get; set; }
    public int WeeklyPaymentCents { get; set; }
    public int TotalRepayCents { get; set; }
    public int TotalInterestCents { get; set; }
    public string Status { get; set; } = LoanStatuses.Pending;
    public string? BankerNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }

    public Account Account { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
    public List<LoanInstallment> Installments { get; set; } = [];
}

public class LoanInstallment
{
    public int Id { get; set; }
    public int LoanRequestId { get; set; }
    public int Sequence { get; set; }
    public DateOnly DueDate { get; set; }
    public int AmountCents { get; set; }
    public int InterestCents { get; set; }
    public int PrincipalCents { get; set; }
    public string Status { get; set; } = LoanInstallmentStatuses.Scheduled;
    public DateTime? PaidAt { get; set; }

    public LoanRequest LoanRequest { get; set; } = null!;
    public Transaction? Transaction { get; set; }
}

public class Transaction
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Type { get; set; } = "";
    public int AmountCents { get; set; }
    public string Note { get; set; } = "";
    public int? TaskSubmissionId { get; set; }
    public int? LoanInstallmentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }

    public Account Account { get; set; } = null!;
    public TaskSubmission? TaskSubmission { get; set; }
    public LoanInstallment? LoanInstallment { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
