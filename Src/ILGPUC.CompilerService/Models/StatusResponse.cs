using System.Text.Json.Serialization;

namespace ILGPUC.CompilerService.Models;

/// <summary>
/// Response model returned by the health-check endpoint.
/// </summary>
public sealed class StatusResponse
{
    /// <summary>
    /// Gets a human-readable health status string (e.g. <c>"healthy"</c>).
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Gets the UTC timestamp at which the status was generated.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the name of the service.
    /// </summary>
    [JsonPropertyName("service")]
    public required string Service { get; init; }
}
