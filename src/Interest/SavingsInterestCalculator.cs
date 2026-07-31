using SandersSavingsAndLoan.Data;

namespace SandersSavingsAndLoan.Interest;

public static class SavingsInterestCalculator
{
    /// <summary>Introductory monthly rate on average daily balance. Adjust in code as needed.</summary>
    public const decimal MonthlyRate = 0.08m;

    public static int ComputeAverageDailyBalanceCents(
        IReadOnlyList<Transaction> transactions,
        DateOnly monthStart,
        DateOnly monthEnd)
    {
        var daysInMonth = monthEnd.DayNumber - monthStart.DayNumber + 1;
        var ordered = transactions
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .ToList();

        var balance = 0;
        var index = 0;

        while (index < ordered.Count && DateOnly.FromDateTime(ordered[index].CreatedAt) < monthStart)
        {
            balance = Apply(balance, ordered[index]);
            index++;
        }

        long sum = 0;
        for (var day = monthStart; day <= monthEnd; day = day.AddDays(1))
        {
            while (index < ordered.Count && DateOnly.FromDateTime(ordered[index].CreatedAt) == day)
            {
                balance = Apply(balance, ordered[index]);
                index++;
            }

            sum += balance;
        }

        return (int)Math.Round((decimal)sum / daysInMonth, MidpointRounding.AwayFromZero);
    }

    public static int ComputeInterestCents(int averageDailyBalanceCents)
    {
        if (averageDailyBalanceCents <= 0)
            return 0;

        return (int)Math.Round(averageDailyBalanceCents * MonthlyRate, MidpointRounding.AwayFromZero);
    }

    public static string FormatInterestNote(int averageDailyBalanceCents, int interestCents)
    {
        return $"Your average balance for last month was {FormatDollars(averageDailyBalanceCents)}, so you earned {FormatDollars(interestCents)} in interest";
    }

    public static string FormatDollars(int cents) =>
        (cents / 100m).ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

    private static int Apply(int balance, Transaction tx) =>
        tx.Type == TransactionTypes.Deposit
            ? balance + tx.AmountCents
            : balance - tx.AmountCents;
}
