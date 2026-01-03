using System.Text.Json.Serialization;

namespace ILGPUC.CompilerService.Models;

/// <summary>
/// Contains a machine-readable code and a human-readable description for
/// an API error.
/// </summary>
public sealed class ErrorDetail
{
    /// <summary>
    /// Gets the machine-readable error code (e.g.
    /// <c>"MISSING_SOURCE"</c>).
    /// </summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

/// <summary>
/// Standard error response envelope returned for all non-2xx responses.
/// </summary>
public sealed class ErrorResponse
{
    /// <summary>
    /// Always <see langword="false"/> to indicate the request failed.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success => false;

    /// <summary>
    /// Gets the structured error detail.
    /// </summary>
    [JsonPropertyName("error")]
    public required ErrorDetail Error { get; init; }
}
