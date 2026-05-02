// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CapabilitiesController.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Compilers;
using Microsoft.AspNetCore.Mvc;

namespace ILGPUC.CompilerService.Controllers;

/// <summary>
/// Provides the capabilities endpoint at
/// <c>GET /api/v1/capabilities</c>.
/// </summary>
public sealed class CapabilitiesController : BaseApiController
{
    private readonly ICompilerManager _compilerManager;

    /// <summary>
    /// Constructs a new capabilities controller.
    /// </summary>
    /// <param name="compilerManager">
    /// The compiler manager used to query available capabilities.
    /// </param>
    public CapabilitiesController(ICompilerManager compilerManager)
    {
        _compilerManager = compilerManager;
    }

    /// <summary>
    /// Returns the set of targets and features supported by the compiler.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    [HttpGet]
    public async Task<ActionResult> GetCapabilities(CancellationToken ct)
    {
        return OkResponse(await _compilerManager.GetCapabilitiesAsync(ct)
            .ConfigureAwait(false));
    }
}
