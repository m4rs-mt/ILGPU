// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: JobsController.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.CompilerService.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace ILGPUC.CompilerService.Controllers;

/// <summary>
/// Provides the job-status endpoint at <c>GET /api/v1/jobs/{jobId}</c>.
/// </summary>
public sealed class JobsController : BaseApiController
{
    private readonly IJobQueue _jobQueue;

    /// <summary>
    /// Constructs a new jobs controller.
    /// </summary>
    /// <param name="jobQueue">The job queue used to look up job state.</param>
    public JobsController(IJobQueue jobQueue)
    {
        _jobQueue = jobQueue;
    }

    /// <summary>
    /// Retrieves the current status and result of a compilation job.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <returns>
    /// A 200 response with job state, or 404 if the job does not exist.
    /// </returns>
    [HttpGet("{jobId}")]
    public ActionResult GetJob(string jobId)
    {
        var job = _jobQueue.GetJob(jobId);

        if (job is null)
            return NotFoundResponse("JOB_NOT_FOUND", $"No job found with ID: {jobId}");

        var (status, result, startedAt, completedAt) = job.GetSnapshot();

        if (status is JobStatus.Queued or JobStatus.Running)
        {
            return OkResponse(new
            {
                jobId = job.JobId,
                status = status.ToString().ToLowerInvariant(),
                createdAt = job.CreatedAt,
                startedAt,
            });
        }

        return OkResponse(new
        {
            jobId = job.JobId,
            status = status.ToString().ToLowerInvariant(),
            createdAt = job.CreatedAt,
            startedAt,
            completedAt,
            result,
        });
    }
}
