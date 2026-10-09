// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AdvancedViewsExecutionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

/// <summary>
/// Family A.1 regression: launcher view-of-struct marshalling. The kernel
/// receives an <c>ArrayView&lt;ComposedView&gt;</c> (a view over a user
/// struct), reinterprets it as bytes, slices to a field offset, casts back
/// to int, and atomically increments. Pins the FlatStructLauncherEmitter
/// fix that landed on <c>temp5</c>.
/// </summary>
public abstract class AdvancedViewsExecutionTests : ExecutionTestBase
{
    protected AdvancedViewsExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task ViewOfStructCounter_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/AdvancedViews/ViewOfStructCounter.cs",
            ["Kernels.ViewOfStructCounterKernel"],
            ["1024"]);
    }
}
