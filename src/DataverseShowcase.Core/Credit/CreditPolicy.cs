using System.Globalization;

namespace DataverseShowcase.Core.Credit;

public enum CreditStatus
{
    /// <summary>The account has no credit limit set.</summary>
    NoLimit = 0,

    /// <summary>Open orders use less than 80% of the limit.</summary>
    WithinLimit = 1,

    /// <summary>Open orders use 80% or more of the limit.</summary>
    NearLimit = 2,

    /// <summary>Open orders exceed the limit.</summary>
    OverLimit = 3,

    /// <summary>The account is on credit hold; no new credit is available.</summary>
    OnHold = 4,
}

/// <summary>Credit position of one customer account.</summary>
public sealed class CreditSummary
{
    public CreditSummary(decimal? creditLimit, decimal openOrdersTotal, decimal? availableCredit, decimal? utilisationPercent, CreditStatus status)
    {
        CreditLimit = creditLimit;
        OpenOrdersTotal = openOrdersTotal;
        AvailableCredit = availableCredit;
        UtilisationPercent = utilisationPercent;
        Status = status;
    }

    public decimal? CreditLimit { get; }

    public decimal OpenOrdersTotal { get; }

    /// <summary>Limit minus open orders, never below zero; <c>null</c> when there is no limit.</summary>
    public decimal? AvailableCredit { get; }

    /// <summary>Open orders as a percentage of the limit; <c>null</c> when there is no (or a zero) limit.</summary>
    public decimal? UtilisationPercent { get; }

    public CreditStatus Status { get; }
}

/// <summary>Outcome of a proposed change to an account's credit limit.</summary>
public sealed class CreditLimitDecision
{
    private CreditLimitDecision(bool isAllowed, string? reason)
    {
        IsAllowed = isAllowed;
        Reason = reason;
    }

    public bool IsAllowed { get; }

    public string? Reason { get; }

    public static CreditLimitDecision Allow() => new(true, null);

    public static CreditLimitDecision Reject(string reason) => new(false, reason);
}

/// <summary>Credit rules, independent of Dataverse so they can be unit tested on their own.</summary>
public static class CreditPolicy
{
    /// <summary>Utilisation at or above this share of the limit counts as "near the limit".</summary>
    public const decimal NearLimitThreshold = 0.8m;

    public static CreditSummary Summarize(decimal? creditLimit, bool onHold, decimal openOrdersTotal)
    {
        if (creditLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(creditLimit), creditLimit, "A credit limit cannot be negative.");
        }

        if (openOrdersTotal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(openOrdersTotal), openOrdersTotal, "Open orders cannot total less than zero.");
        }

        decimal? utilisation = creditLimit > 0 ? Math.Round(openOrdersTotal / creditLimit.Value * 100m, 1) : null;
        decimal? available = creditLimit is { } limit ? Math.Max(0m, limit - openOrdersTotal) : null;

        if (onHold)
        {
            return new CreditSummary(creditLimit, openOrdersTotal, 0m, utilisation, CreditStatus.OnHold);
        }

        if (creditLimit is null)
        {
            return new CreditSummary(null, openOrdersTotal, null, null, CreditStatus.NoLimit);
        }

        var status = openOrdersTotal > creditLimit.Value
            ? CreditStatus.OverLimit
            : openOrdersTotal >= creditLimit.Value * NearLimitThreshold
                ? CreditStatus.NearLimit
                : CreditStatus.WithinLimit;

        return new CreditSummary(creditLimit, openOrdersTotal, available, utilisation, status);
    }

    /// <summary>
    /// A credit limit can be raised or removed at any time, but not lowered below what the customer
    /// already has on open orders. That would put the account over its limit without anyone noticing.
    /// </summary>
    public static CreditLimitDecision ValidateLimitChange(decimal? newCreditLimit, decimal openOrdersTotal)
    {
        if (newCreditLimit is null)
        {
            return CreditLimitDecision.Allow();
        }

        if (newCreditLimit < 0)
        {
            return CreditLimitDecision.Reject("The credit limit cannot be negative.");
        }

        if (newCreditLimit < openOrdersTotal)
        {
            return CreditLimitDecision.Reject(string.Format(
                CultureInfo.InvariantCulture,
                "The credit limit ({0:N2}) cannot be lower than the customer's open orders ({1:N2}). " +
                "Fulfil or cancel orders first, or choose a limit of at least {1:N2}.",
                newCreditLimit.Value,
                openOrdersTotal));
        }

        return CreditLimitDecision.Allow();
    }
}
