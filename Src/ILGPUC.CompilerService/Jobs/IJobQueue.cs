// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IJobQueue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

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
