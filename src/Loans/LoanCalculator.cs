namespace SandersSavingsAndLoan.Loans;

public static class LoanCalculator
{
    public const double WeeklyRate = 0.15;
    public static readonly int[] AllowedTerms = [2, 3, 4, 6, 8, 10];

    public static bool IsAllowedTerm(int termWeeks) => AllowedTerms.Contains(termWeeks);

    public static LoanScheduleResult BuildSchedule(int principalCents, int termWeeks, DateOnly startDate)
    {
        if (principalCents <= 0)
            throw new ArgumentOutOfRangeException(nameof(principalCents), "Principal must be positive.");
        if (!IsAllowedTerm(termWeeks))
            throw new ArgumentOutOfRangeException(nameof(termWeeks), "Term is not an allowed option.");

        var weeklyPaymentCents = ComputeWeeklyPaymentCents(principalCents, termWeeks);
        var remaining = principalCents;
        var rows = new List<LoanScheduleRow>(termWeeks);

        for (var i = 1; i <= termWeeks; i++)
        {
            var dueDate = startDate.AddDays(7 * i);
            var interestCents = (int)Math.Round(remaining * WeeklyRate, MidpointRounding.AwayFromZero);

            int paymentCents;
            int principalPortion;
            if (i == termWeeks)
            {
                // Final payment clears remaining principal + interest for this period.
                principalPortion = remaining;
                paymentCents = remaining + interestCents;
            }
            else
            {
                paymentCents = weeklyPaymentCents;
                principalPortion = paymentCents - interestCents;
                if (principalPortion < 0)
                    principalPortion = 0;
                if (principalPortion > remaining)
                    principalPortion = remaining;
                paymentCents = principalPortion + interestCents;
            }

            rows.Add(new LoanScheduleRow(i, dueDate, paymentCents, interestCents, principalPortion));
            remaining -= principalPortion;
        }

        var totalRepay = rows.Sum(r => r.AmountCents);
        var totalInterest = totalRepay - principalCents;

        return new LoanScheduleResult(
            WeeklyPaymentCents: weeklyPaymentCents,
            TotalRepayCents: totalRepay,
            TotalInterestCents: totalInterest,
            Schedule: rows);
    }

    /// <summary>
    /// Standard amortizing payment in cents, rounded up so early payments never under-collect.
    /// </summary>
    public static int ComputeWeeklyPaymentCents(int principalCents, int termWeeks)
    {
        var r = WeeklyRate;
        var n = termWeeks;
        var factor = Math.Pow(1 + r, n);
        var payment = principalCents * r * factor / (factor - 1);
        return (int)Math.Ceiling(payment);
    }
}

public record LoanScheduleRow(
    int Sequence,
    DateOnly DueDate,
    int AmountCents,
    int InterestCents,
    int PrincipalCents);

public record LoanScheduleResult(
    int WeeklyPaymentCents,
    int TotalRepayCents,
    int TotalInterestCents,
    IReadOnlyList<LoanScheduleRow> Schedule);
