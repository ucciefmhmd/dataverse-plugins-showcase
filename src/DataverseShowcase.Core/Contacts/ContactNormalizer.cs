using System.Text;

namespace DataverseShowcase.Core.Contacts;

/// <summary>Result of normalising one field: the cleaned value, or the reason it was rejected.</summary>
public sealed class NormalizationResult
{
    private NormalizationResult(string? value, string? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>The normalised value; <c>null</c> means the field was cleared.</summary>
    public string? Value { get; }

    public string? Error { get; }

    public bool IsValid => Error is null;

    public static NormalizationResult Valid(string? value) => new(value, null);

    public static NormalizationResult Invalid(string error) => new(null, error);
}

/// <summary>
/// Contact data-quality rules. Keeping email addresses and phone numbers in one canonical form is what
/// makes duplicate detection, matching against other systems, and click-to-call work reliably.
/// </summary>
public static class ContactNormalizer
{
    private const int MinPhoneDigits = 7;
    private const int MaxPhoneDigits = 15; // E.164 maximum

    /// <summary>Trims and lower-cases an email address and checks its basic shape.</summary>
    public static NormalizationResult NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return NormalizationResult.Valid(null);
        }

        var candidate = email!.Trim().ToLowerInvariant();
        var at = candidate.IndexOf('@');
        var domain = at > 0 ? candidate.Substring(at + 1) : string.Empty;

        var looksValid =
            at > 0
            && at == candidate.LastIndexOf('@')
            && domain.Length >= 3
            && domain.IndexOf('.') > 0
            && !domain.EndsWith(".", StringComparison.Ordinal)
            && !domain.Contains("..")
            && candidate.All(c => c > ' ' && c < 127);

        return looksValid
            ? NormalizationResult.Valid(candidate)
            : NormalizationResult.Invalid($"'{email.Trim()}' is not a valid email address.");
    }

    /// <summary>
    /// Reduces a phone number to digits with an optional leading '+'. A leading international "00"
    /// prefix becomes '+'. Separators such as spaces, dashes, dots and brackets are removed.
    /// </summary>
    public static NormalizationResult NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return NormalizationResult.Valid(null);
        }

        var trimmed = phone!.Trim();
        var builder = new StringBuilder(trimmed.Length);
        var international = trimmed.StartsWith("+", StringComparison.Ordinal);

        foreach (var c in trimmed)
        {
            if (char.IsDigit(c))
            {
                builder.Append(c);
            }
            else if (c is not (' ' or '-' or '.' or '(' or ')' or '+' or '/'))
            {
                return NormalizationResult.Invalid($"'{trimmed}' is not a valid phone number.");
            }
        }

        var digits = builder.ToString();
        if (!international && digits.StartsWith("00", StringComparison.Ordinal))
        {
            international = true;
            digits = digits.Substring(2);
        }

        if (digits.Length is < MinPhoneDigits or > MaxPhoneDigits)
        {
            return NormalizationResult.Invalid(
                $"'{trimmed}' is not a valid phone number (expected {MinPhoneDigits}–{MaxPhoneDigits} digits).");
        }

        return NormalizationResult.Valid(international ? "+" + digits : digits);
    }
}
