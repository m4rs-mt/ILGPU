// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StatusController.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.CompilerService.Models;
using Microsoft.AspNetCore.Mvc;

namespace ILGPUC.CompilerService.Controllers;

/// <summary>
/// Provides the health-check endpoint at <c>GET /api/v1/status</c>.
/// </summary>
public sealed class StatusController : BaseApiController
{
    /// <summary>
    /// Returns the current health status of the service.
    /// </summary>
    [HttpGet]
    public ActionResult GetStatus()
    {
        return OkResponse(new StatusResponse
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Service = "ILGPUC.CompilerService",
        });
    }
}
