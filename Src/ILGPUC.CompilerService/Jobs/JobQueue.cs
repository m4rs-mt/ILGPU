using System.Collections.Concurrent;
using ILGPU.Util;
using ILGPUC.Compilers;

namespace ILGPUC.CompilerService.Jobs;

/// <summary>
/// Manages asynchronous compilation jobs, executing them in the background
/// and cleaning up expired entries automatically.
/// </summary>
public sealed class JobQueue : DisposeBase, IJobQueue
{
    private static readonly TimeSpan JobRetention = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, CompilationJob> _jobs = new();
    private readonly ICompilerManager _compilerManager;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// Constructs a new job queue.
    /// </summary>
    /// <param name="compilerManager">
    /// The compiler manager used to execute compilation requests.
    /// </param>
    public JobQueue(ICompilerManager compilerManager)
    {
        _compilerManager = compilerManager;
        _cleanupTimer = new Timer(
            _ => CleanupExpiredJobs(),
            state: null,
            dueTime: TimeSpan.FromMinutes(5),
            period: TimeSpan.FromMinutes(5));
    }

    /// <inheritdoc/>
    public string Enqueue(CompileRequest request)
    {
        VerifyNotDisposed();

        var jobId = request.JobId ?? Guid.NewGuid().ToString("N");

        if (!_jobs.TryAdd(jobId, null!))
            throw new InvalidOperationException($"Job ID '{jobId}' already exists");

        var job = new CompilationJob
        {
            JobId = jobId,
            Request = request,
            Status = JobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
        };

        _jobs[jobId] = job;

        _ = RunJobAsync(job);

        return jobId;
    }

    /// <inheritdoc/>
    public CompilationJob? GetJob(string jobId)
    {
        VerifyNotDisposed();

        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shutdownCts.Cancel();
            _cleanupTimer.Dispose();
            _shutdownCts.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Executes the given job asynchronously, updating its status and result
    /// when the compilation finishes or is cancelled.
    /// </summary>
    /// <param name="job">The job to run.</param>
    private async Task RunJobAsync(CompilationJob job)
    {
        job.Status = JobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        try
        {
            var result = await _compilerManager
                .CompileAsync(job.Request, _shutdownCts.Token)
                .ConfigureAwait(false);
            job.Result = result;
            job.Status = result.Success ? JobStatus.Completed : JobStatus.Failed;
        }
        catch (OperationCanceledException)
        {
            job.Result = new CompilationResult
            {
                Success = false,
                StdErr = "Compilation cancelled due to server shutdown",
                ExitCode = -1,
                Target = job.Request.Target,
                OutputType = job.Request.OutputType,
            };
            job.Status = JobStatus.Failed;
        }
        catch (Exception ex)
        {
            job.Result = new CompilationResult
            {
                Success = false,
                StdErr = ex.Message,
                ExitCode = -1,
                Target = job.Request.Target,
                OutputType = job.Request.OutputType,
            };
            job.Status = JobStatus.Failed;
        }
        finally
        {
            job.CompletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Removes completed and failed jobs that are older than
    /// <see cref="JobRetention"/>.
    /// </summary>
    private void CleanupExpiredJobs()
    {
        var cutoff = DateTime.UtcNow - JobRetention;
        foreach (var kvp in _jobs)
        {
            var (status, _, _, completedAt) = kvp.Value.GetSnapshot();
            if (status is JobStatus.Completed or JobStatus.Failed
                && completedAt.HasValue
                && completedAt.Value < cutoff)
            {
                _jobs.TryRemove(kvp.Key, out _);
            }
        }
    }
}
