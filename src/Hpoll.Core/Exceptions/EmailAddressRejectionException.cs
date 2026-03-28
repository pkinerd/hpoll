namespace Hpoll.Core.Exceptions;

/// <summary>
/// Thrown when an email send fails because one or more recipient addresses
/// were rejected or deemed invalid by the mail service (e.g. unverified
/// address in SES sandbox, malformed address format). Distinguished from
/// transient network/service failures so callers can apply targeted retry
/// or fallback logic.
/// </summary>
public class EmailAddressRejectionException : Exception
{
    public EmailAddressRejectionException(string message, Exception innerException)
        : base(message, innerException) { }
}
