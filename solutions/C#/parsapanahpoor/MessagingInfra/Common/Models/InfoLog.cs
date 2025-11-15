namespace MessagingInfra.Common.Models;

/// <summary>
/// Represents an informational log that broadcasts to all subscribers
/// </summary>
public record InfoLog(
    string Id,
    string Service,
    string Message,
    int LatencyMs,
    DateTime Timestamp
);
