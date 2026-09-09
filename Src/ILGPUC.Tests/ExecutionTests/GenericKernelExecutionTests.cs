// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericKernelExecutionTests.cs
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
/// Family A.2 regression: generic-kernel struct-closure marshalling. The
/// kernel is generic over a closure type that captures field state; the
/// launcher must marshal the captured field through to the device. Pins
/// the FlatStructLauncherEmitter regression that landed on <c>temp5</c>.
/// </summary>
public abstract class GenericKernelExecutionTests : ExecutionTestBase
{
    protected GenericKernelExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task GenericClosure_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GenericKernel/GenericClosure.cs",
            ["Kernels.LaunchClosureKernel"],
            ["20", "21", "22", "23"]);
    }

    /// <summary>
    /// Generic kernels launched from generic wrappers three levels deep, from a
    /// method of a generic class, with a constructed type argument, and directly
    /// with closed type arguments, all in one program.
    /// </summary>
    [SkippableFact]
    public async Task NestedWrappers_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GenericKernel/NestedWrappers.cs",
            ["Kernels.Scale"],
            [
                "2", "4", "6", "8",
                "3", "6", "9", "12",
                "30", "60", "90", "120",
                "2", "1", "4", "3",
                "5", "10", "15", "20",
                "7", "14", "21", "28",
            ]);
    }

    /// <summary>
    /// Named kernel arguments in a different order and omitted optional
    /// arguments bind to the right parameters, directly and through a wrapper.
    /// </summary>
    [SkippableFact]
    public async Task NamedArguments_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GenericKernel/NamedArguments.cs",
            ["Kernels.Subtract"],
            ["10", "20", "13", "23"]);
    }

    /// <summary>
    /// Overloaded kernels in a generic class resolve to the right overload for
    /// every type argument.
    /// </summary>
    [SkippableFact]
    public async Task OverloadedKernels_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GenericKernel/OverloadedKernels.cs",
            ["Ops.Scale"],
            ["2", "4", "3", "6", "10", "20"]);
    }

    /// <summary>
    /// Type arguments nested in generic types, from namespaces shadowed at the
    /// launch site and value tuples are named correctly in the generated code.
    /// </summary>
    [SkippableFact]
    public async Task ShadowedNames_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GenericKernel/ShadowedNames.cs",
            ["Kernels.Store"],
            ["3", "5", "7"]);
    }

    /// <summary>
    /// Inline lambda kernels in a generic method and in a method of a generic
    /// class, and a local function kernel of a generic method, are compiled for
    /// every instantiation of the code that contains them.
    /// </summary>
    [SkippableFact]
    public async Task LocalKernels_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GenericKernel/LocalKernels.cs",
            [],
            [
                "1 1 1 1",
                "2.5 2.5 2.5 2.5",
                "3 4 3 4",
                "0.5 1.5 0.5 1.5",
                "7 7 8 8",
                "5 5 6 6",
            ]);
    }

    /// <summary>
    /// Generic methods that pass lambda literals to a kernel taking a delegate,
    /// plain and capturing a value of a type parameter, compile the lifted launch
    /// for every instantiation of the method.
    /// </summary>
    [SkippableFact]
    public async Task DelegateKernels_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GenericKernel/DelegateKernels.cs",
            [],
            ["2 4 6", "1 3 5", "11 12 13", "10.5 11.5 12.5"]);
    }
}
