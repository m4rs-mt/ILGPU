// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilationJob.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Compilers;

namespace ILGPUC.CompilerService.Jobs;

/// <summary>
/// Represents the lifecycle state of a compilation job.
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// The job is waiting to be processed.
    /// </summary>
    Queued,

    /// <summary>
    /// The job is currently being compiled.
    /// </summary>
    Running,

    /// <summary>
    /// The job finished successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The job finished with a compilation or runtime error.
    /// </summary>
    Failed,
}

/// <summary>
/// Represents a single asynchronous compilation job and its thread-safe
/// mutable state.
/// </summary>
public sealed class CompilationJob
{
    private readonly object _lock = new();
    private JobStatus _status;
    private CompilationResult? _result;
    private DateTime? _startedAt;
    private DateTime? _completedAt;

    /// <summary>
    /// Gets the unique identifier for this job.
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// Gets the original compile request associated with this job.
    /// </summary>
    public required CompileRequest Request { get; init; }

    /// <summary>
    /// Gets the UTC timestamp at which this job was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets or sets the current status of the job.
    /// </summary>
    public JobStatus Status
    {
        get { lock (_lock) return _status; }
        set { lock (_lock) _status = value; }
    }

    /// <summary>
    /// Gets or sets the compilation result, available once the job has
    /// completed or failed.
    /// </summary>
    public CompilationResult? Result
    {
        get { lock (_lock) return _result; }
        set { lock (_lock) _result = value; }
    }

    /// <summary>
    /// Gets or sets the UTC timestamp at which the job started running.
    /// </summary>
    public DateTime? StartedAt
    {
        get { lock (_lock) return _startedAt; }
        set { lock (_lock) _startedAt = value; }
    }

    /// <summary>
    /// Gets or sets the UTC timestamp at which the job finished.
    /// </summary>
    public DateTime? CompletedAt
    {
        get { lock (_lock) return _completedAt; }
        set { lock (_lock) _completedAt = value; }
    }

    /// <summary>
    /// Atomically reads all mutable state fields and returns them as a
    /// single snapshot.
    /// </summary>
    /// <returns>
    /// A tuple containing the current status, result, started-at timestamp,
    /// and completed-at timestamp.
    /// </returns>
    public (
        JobStatus status,
        CompilationResult? result,
        DateTime? startedAt,
        DateTime? completedAt) GetSnapshot()
    {
        lock (_lock)
            return (_status, _result, _startedAt, _completedAt);
    }
}
