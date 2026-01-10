using ILGPUC.Compilers;

namespace ILGPUC.CompilerService.Jobs;

/// <summary>
/// Represents a queue for asynchronous compilation jobs.
/// </summary>
public interface IJobQueue : IDisposable
{
    /// <summary>
    /// Enqueues a compilation request for asynchronous processing.
    /// </summary>
    /// <param name="request">The compile request to enqueue.</param>
    /// <returns>The unique job ID assigned to the request.</returns>
    string Enqueue(CompileRequest request);

    /// <summary>
    /// Retrieves a queued or completed job by its ID.
    /// </summary>
    /// <param name="jobId">The job ID to look up.</param>
    /// <returns>
    /// The job, or <see langword="null"/> if no job with that ID exists.
    /// </returns>
    CompilationJob? GetJob(string jobId);
}
