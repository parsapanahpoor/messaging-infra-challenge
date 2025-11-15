namespace MessagingInfra.Common.Models;

/// <summary>
/// Represents a critical error log that requires processing by exactly one worker
/// </summary>
public record ErrorLog(
    string Id,
    string Service,
    string Message,
    string Severity,
    DateTime Timestamp
);
