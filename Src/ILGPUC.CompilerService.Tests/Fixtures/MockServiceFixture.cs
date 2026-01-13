// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MockServiceFixture.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Compilers;
using ILGPUC.CompilerService.Controllers;
using ILGPUC.CompilerService.Tests.Mocks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ILGPUC.CompilerService.Tests.Fixtures;

/// <summary>
/// Hosts the CompilerService in-process with a <see cref="MockCompilerManager"/>
/// replacing the real compiler infrastructure. Shared across test classes via
/// <see cref="IClassFixture{TFixture}"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="StatusController"/> as the assembly marker type because the
/// synthesized <c>Program</c> class from top-level statements is <c>internal</c>
/// in .NET 10 / C# 13 and cannot be made public.
/// </remarks>
public class MockServiceFixture : WebApplicationFactory<StatusController>
{
    /// <summary>
    /// The mock compiler manager instance. Tests can inspect
    /// <see cref="MockCompilerManager.CompileCallCount"/> and configure
    /// <see cref="MockCompilerManager.ForceFailureMessage"/> between tests.
    /// </summary>
    public MockCompilerManager MockManager { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real CompilerManager registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ICompilerManager));
            if (descriptor is not null)
                services.Remove(descriptor);

            // Inject mock
            services.AddSingleton<ICompilerManager>(MockManager);
        });
    }
}
