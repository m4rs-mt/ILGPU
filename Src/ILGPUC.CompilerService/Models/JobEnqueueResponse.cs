using System.Text.Json.Serialization;

namespace ILGPUC.CompilerService.Models;

/// <summary>
/// Response returned when a compilation job is enqueued for asynchronous
/// processing.
/// </summary>
public sealed class JobEnqueueResponse
{
    /// <summary>
    /// Gets the unique identifier assigned to the queued job.
    /// </summary>
    [JsonPropertyName("jobId")]
    public required string JobId { get; init; }

    /// <summary>
    /// Gets the initial status of the job, always <c>"queued"</c>.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "queued";
}
