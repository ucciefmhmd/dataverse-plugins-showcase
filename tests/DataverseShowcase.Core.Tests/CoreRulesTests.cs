using DataverseShowcase.Core.Contacts;
using DataverseShowcase.Core.Credit;

namespace DataverseShowcase.Core.Tests;

public class CreditPolicyTests
{
    [Theory]
    [InlineData(50_000, 10_000, CreditStatus.WithinLimit, 40_000, 20.0)]
    [InlineData(50_000, 40_000, CreditStatus.NearLimit, 10_000, 80.0)]   // exactly 80%: near
    [InlineData(50_000, 50_000, CreditStatus.NearLimit, 0, 100.0)]      // at the limit, not over
    [InlineData(50_000, 50_001, CreditStatus.OverLimit, 0, 100.0)]
    public void Status_follows_utilisation(int limit, int open, CreditStatus status, int available, double utilisation)
    {
        var summary = CreditPolicy.Summarize(limit, onHold: false, openOrdersTotal: open);

        Assert.Equal(status, summary.Status);
        Assert.Equal(available, summary.AvailableCredit);
        Assert.Equal((decimal)utilisation, summary.UtilisationPercent);
    }

    [Fact]
    public void Hold_overrides_everything_and_leaves_no_credit()
    {
        var summary = CreditPolicy.Summarize(100_000m, onHold: true, openOrdersTotal: 0m);

        Assert.Equal(CreditStatus.OnHold, summary.Status);
        Assert.Equal(0m, summary.AvailableCredit);
    }

    [Fact]
    public void No_limit_means_no_available_credit_figure()
    {
        var summary = CreditPolicy.Summarize(null, onHold: false, openOrdersTotal: 1_234m);

        Assert.Equal(CreditStatus.NoLimit, summary.Status);
        Assert.Null(summary.AvailableCredit);
        Assert.Null(summary.UtilisationPercent);
    }

    [Fact]
    public void Zero_limit_has_no_utilisation_percentage()
    {
        var summary = CreditPolicy.Summarize(0m, onHold: false, openOrdersTotal: 0m);

        Assert.Null(summary.UtilisationPercent);
        Assert.Equal(CreditStatus.NearLimit, summary.Status);
    }

    [Fact]
    public void Negative_inputs_are_programming_errors()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreditPolicy.Summarize(-1m, false, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreditPolicy.Summarize(1m, false, -1m));
    }

    [Theory]
    [InlineData(null, true)]       // clearing the limit
    [InlineData(42_500.0, true)]   // equal to open orders
    [InlineData(42_499.99, false)]
    [InlineData(-5.0, false)]
    public void Limit_changes_cannot_go_below_open_orders(double? newLimit, bool allowed)
    {
        var decision = CreditPolicy.ValidateLimitChange((decimal?)newLimit, 42_500m);

        Assert.Equal(allowed, decision.IsAllowed);
        Assert.Equal(allowed, decision.Reason is null);
    }
}

public class ContactNormalizerTests
{
    [Theory]
    [InlineData("  Sara.Ahmed@Contoso.EXAMPLE ", "sara.ahmed@contoso.example")]
    [InlineData("a@b.co", "a@b.co")]
    public void Valid_emails_are_trimmed_and_lower_cased(string input, string expected) =>
        Assert.Equal(expected, ContactNormalizer.NormalizeEmail(input).Value);

    [Theory]
    [InlineData("no-at-sign.example")]
    [InlineData("two@@contoso.example")]
    [InlineData("@contoso.example")]
    [InlineData("sara@localhost")]
    [InlineData("sara@contoso.")]
    [InlineData("sara@contoso..example")]
    [InlineData("sara ahmed@contoso.example")]
    public void Malformed_emails_are_rejected(string input)
    {
        var result = ContactNormalizer.NormalizeEmail(input);

        Assert.False(result.IsValid);
        Assert.Contains("not a valid email address", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_values_clear_the_field(string? input)
    {
        Assert.True(ContactNormalizer.NormalizeEmail(input).IsValid);
        Assert.Null(ContactNormalizer.NormalizeEmail(input).Value);
        Assert.Null(ContactNormalizer.NormalizePhone(input).Value);
    }

    [Theory]
    [InlineData("+20 100 123 4567", "+201001234567")]
    [InlineData("0020 (100) 123-4567", "+201001234567")]  // 00 international prefix
    [InlineData("02.2345.6789", "0223456789")]           // national number keeps its leading 0
    [InlineData("+44 20 7946 0958", "+442079460958")]
    public void Phones_are_reduced_to_digits_with_an_optional_plus(string input, string expected) =>
        Assert.Equal(expected, ContactNormalizer.NormalizePhone(input).Value);

    [Theory]
    [InlineData("12345")]                 // too short
    [InlineData("+1234567890123456")]     // longer than E.164 allows
    [InlineData("ext. 42 or 0100123")]    // letters
    public void Invalid_phones_are_rejected(string input) =>
        Assert.False(ContactNormalizer.NormalizePhone(input).IsValid);
}
