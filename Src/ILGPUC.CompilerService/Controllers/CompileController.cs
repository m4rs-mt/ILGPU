// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompileController.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Compilers;
using ILGPUC.CompilerService.Caching;
using ILGPUC.CompilerService.Jobs;
using ILGPUC.CompilerService.Models;
using Microsoft.AspNetCore.Mvc;

namespace ILGPUC.CompilerService.Controllers;

/// <summary>
/// Provides the compilation endpoint at <c>POST /api/v1/compile</c>.
/// Supports both synchronous and asynchronous (job-queued) compilation modes.
/// </summary>
public sealed class CompileController : BaseApiController
{
    private readonly ICompilerManager _compilerManager;
    private readonly ICache _cache;
    private readonly IJobQueue _jobQueue;

    /// <summary>
    /// Constructs a new compile controller.
    /// </summary>
    /// <param name="compilerManager">
    /// The compiler manager used for synchronous compilation.
    /// </param>
    /// <param name="cache">The result cache.</param>
    /// <param name="jobQueue">
    /// The job queue used for asynchronous compilation.
    /// </param>
    public CompileController(
        ICompilerManager compilerManager,
        ICache cache,
        IJobQueue jobQueue)
    {
        _compilerManager = compilerManager;
        _cache = cache;
        _jobQueue = jobQueue;
    }

    /// <summary>
    /// Compiles source code according to the given request.
    /// </summary>
    /// <remarks>
    /// When <c>AsyncMode</c> is set on the request the method enqueues a
    /// background job and returns the job ID immediately. Otherwise the
    /// compilation runs synchronously and the result is returned directly,
    /// served from cache when available.
    /// </remarks>
    /// <param name="request">The compilation request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="JobEnqueueResponse"/> in async mode, or a
    /// <see cref="CompilationResult"/> in sync mode.
    /// </returns>
    [HttpPost]
    public async Task<ActionResult> Compile(
        [FromBody] CompileRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SourceCode))
            return BadRequestResponse("MISSING_SOURCE", "sourceCode is required");

        if (!Enum.IsDefined(request.Target))
            return BadRequestResponse(
                "INVALID_TARGET", $"Unknown target: {request.Target}");

        // Async mode — enqueue and return job ID
        if (request.AsyncMode)
        {
            var jobId = _jobQueue.Enqueue(request);
            return OkResponse(new JobEnqueueResponse { JobId = jobId });
        }

        // Check cache
        var cacheKey = CompilationCache.ComputeKey(request);
        var cached = _cache.Get(cacheKey);
        if (cached is not null)
        {
            return OkResponse(new CompilationResult
            {
                Success = cached.Success,
                Output = cached.Output,
                StdOut = cached.StdOut,
                StdErr = cached.StdErr,
                ExitCode = cached.ExitCode,
                Target = cached.Target,
                OutputType = cached.OutputType,
                IsCached = true,
            });
        }

        // Synchronous compilation
        var result = await _compilerManager.CompileAsync(request, ct)
            .ConfigureAwait(false);

        if (result.Success)
            _cache.Set(cacheKey, result);

        return OkResponse(result);
    }
}
