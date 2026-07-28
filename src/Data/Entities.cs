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
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }

    public Account Account { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
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
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }

    public Account Account { get; set; } = null!;
    public TaskSubmission? TaskSubmission { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
