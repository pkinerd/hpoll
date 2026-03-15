namespace Hpoll.Core.Utilities;

/// <summary>
/// Masks email addresses for safe inclusion in log messages, replacing
/// most of the local part with asterisks while preserving the domain.
/// </summary>
public static class EmailMasker
{
    /// <summary>
    /// Masks a single email address (e.g. "user@example.com" → "us**@example.com").
    /// Returns "***" for addresses with no local part or missing '@'.
    /// </summary>
    public static string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        var local = email[..at];
        var domain = email[at..];
        var visible = Math.Min(2, local.Length);
        return local[..visible] + new string('*', Math.Max(0, local.Length - visible)) + domain;
    }

    /// <summary>
    /// Masks a comma-delimited list of email addresses.
    /// Returns the original value for null or empty input.
    /// </summary>
    public static string MaskList(string email)
    {
        if (string.IsNullOrEmpty(email)) return email;
        var parts = email.Split(',');
        return string.Join(", ", parts.Select(e => Mask(e.Trim())));
    }
}
