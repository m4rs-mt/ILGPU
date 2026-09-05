// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LambdaExecutionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using RoslynOptimizationLevel = Microsoft.CodeAnalysis.OptimizationLevel;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class LambdaExecutionTests : ExecutionTestBase
{
    protected LambdaExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task SimpleCapture_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/SimpleCapture.cs",
            ["Kernels.SimpleCaptureKernel"],
            ["10", "11", "12", "13"]);
    }

    /// <summary>
    /// A local function capturing a parameter and a local of the kernel, which
    /// the C# compiler passes through a closure struct with auto layout.
    /// </summary>
    [SkippableFact]
    public async Task LocalFunctionCapture_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/LocalFunctionCapture.cs",
            ["Kernels.LocalFunctionCaptureKernel"],
            ["5", "15", "25", "35"]);
    }

    /// <summary>
    /// A lambda capturing locals of two nested scopes, whose inner closure class
    /// refers to the outer one through a field in debug builds. Regression guard:
    /// that field was counted as all fields of the outer closure class, which
    /// shifted the indices of the fields behind it in the inner closure class.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release)]
    [InlineData(RoslynOptimizationLevel.Debug)]
    public async Task NestedScopeCapture_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/NestedScopeCapture.cs",
            ["Kernels.NestedScopeCaptureKernel"],
            ["3", "15", "27", "39"],
            programOptimization: programOptimization);
    }

    [SkippableFact]
    public async Task MultiCapture_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/MultiCapture.cs",
            ["Kernels.MultiCaptureKernel"],
            ["2", "5", "8", "11"]);
    }

    /// <summary>
    /// Two different lambdas whose delegate handles cross a block boundary. Each
    /// call site must devirtualize to its own lambda body.
    /// </summary>
    [SkippableFact]
    public async Task TwoLambdasCrossBlock_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/TwoLambdasCrossBlock.cs",
            ["Kernels.TwoLambdasCrossBlockKernel"],
            ["1", "4", "7", "10"]);
    }

    /// <summary>
    /// Two closures whose captured values are read from blocks other than the entry
    /// block. Regression guard — this shape produced 0 for both captures on
    /// every backend, and no execution test covered it (single-capture closures never
    /// reached the offending pass, and the single-block MultiCapture shape read before
    /// the misplaced allocas).
    /// </summary>
    [SkippableFact]
    public async Task TwoClosuresCrossBlock_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/TwoClosuresCrossBlock.cs",
            ["Kernels.TwoClosuresCrossBlockKernel"],
            ["29", "14", "18", "22"]);
    }

    [SkippableFact]
    public async Task LambdaConditional_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/LambdaConditional.cs",
            ["Kernels.LambdaConditionalKernel"],
            ["0", "1", "2", "2"]);
    }

    [SkippableFact]
    public async Task LambdaAppliedTwice_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/LambdaAppliedTwice.cs",
            ["Kernels.LambdaAppliedTwiceKernel"],
            ["11", "13", "15", "17"]);
    }

    [SkippableFact]
    public async Task AccumulateClosure_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/AccumulateClosure.cs",
            ["Kernels.AccumulateClosureKernel"],
            ["1", "3", "5", "7"]);
    }

    [SkippableFact]
    public async Task ClosureReadAfterWrite_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/ClosureReadAfterWrite.cs",
            ["Kernels.ClosureReadAfterWriteKernel"],
            ["0", "2", "4", "6"]);
    }

    /// <summary>
    /// A delegate chosen by a condition dispatches on the chosen lambda, run
    /// with both conditions.
    /// </summary>
    [SkippableFact]
    public async Task SelectedDelegate_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/SelectedDelegate.cs",
            ["Kernels.SelectedDelegateKernel"],
            ["0", "2", "4", "6", "1", "2", "3", "4"]);
    }

    /// <summary>
    /// A delegate chosen by a condition is passed to a helper taking a Func,
    /// which dispatches on the chosen lambda to the specialized helpers.
    /// </summary>
    [SkippableFact]
    public async Task SelectedDelegateArgument_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/SelectedDelegateArgument.cs",
            ["Kernels.SelectedDelegateArgumentKernel"],
            ["0", "2", "4", "6", "10", "11", "12", "13"]);
    }

    /// <summary>
    /// Delegates selected by a condition are invoked and passed to a helper,
    /// where one closure is only created in one branch (passing the selecting
    /// handle as closure) and where two targets share one closure.
    /// </summary>
    [SkippableFact]
    public async Task MergedClosureDelegate_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/MergedClosureDelegate.cs",
            ["Kernels.BranchClosureKernel", "Kernels.SharedClosureKernel"],
            [
                "0", "202", "404", "606",
                "1111", "1212", "1313", "1414",
                "-1010", "-909", "-808", "-707",
                "1010", "1111", "1212", "1313",
            ]);
    }

    /// <summary>
    /// A delegate reassigned in a loop by nested conditionals selects between
    /// three lambdas when it is invoked or passed to a helper in the loop and
    /// when it is invoked after the loop.
    /// </summary>
    [SkippableFact]
    public async Task LoopSelectedDelegate_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/LoopSelectedDelegate.cs",
            ["Kernels.LoopSelectedDelegateKernel"],
            [
                "1", "2", "3", "4",
                "200", "202", "204", "206",
                "600", "601", "604", "609",
                "1400", "1401", "1404", "1409",
            ]);
    }

    /// <summary>
    /// A helper taking a Func is called with a non-capturing lambda.
    /// </summary>
    [SkippableFact]
    public async Task DelegateArgument_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/DelegateArgument.cs",
            ["Kernels.DelegateArgumentKernel"],
            ["0", "3", "6", "9"]);
    }

    /// <summary>
    /// A helper taking a Func is called with a capturing lambda, whose closure
    /// is passed on to the specialized helper.
    /// </summary>
    [SkippableFact]
    public async Task CapturingDelegateArgument_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/CapturingDelegateArgument.cs",
            ["Kernels.CapturingDelegateArgumentKernel"],
            ["10", "11", "12", "13"]);
    }

    /// <summary>
    /// A helper taking a Func is called with a static method group.
    /// </summary>
    [SkippableFact]
    public async Task MethodGroupDelegateArgument_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/MethodGroupDelegateArgument.cs",
            ["Kernels.MethodGroupDelegateArgumentKernel"],
            ["1", "2", "5", "10"]);
    }

    /// <summary>
    /// A helper forwards its Func parameter to another helper.
    /// </summary>
    [SkippableFact]
    public async Task ForwardedDelegateArgument_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/ForwardedDelegateArgument.cs",
            ["Kernels.ForwardedDelegateArgumentKernel"],
            ["10", "11", "12", "13"]);
    }

    /// <summary>
    /// Two different lambdas are passed to the same helper, which is
    /// specialized once per lambda.
    /// </summary>
    [SkippableFact]
    public async Task TwoDelegateArguments_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/TwoDelegateArguments.cs",
            ["Kernels.TwoDelegateArgumentsKernel"],
            ["10", "14", "18", "22"]);
    }

    [SkippableFact]
    public async Task ThreeCapturesLoop_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/ThreeCapturesLoop.cs",
            ["Kernels.ThreeCapturesLoopKernel"],
            ["-1", "-1", "0", "2"]);
    }

    /// <summary>
    /// A helper reassigns its delegate parameter in a branch and invokes it
    /// after the merge, which dispatches on the selected target.
    /// </summary>
    [SkippableFact]
    public async Task DelegateParameterReassigned_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/DelegateParameterReassigned.cs",
            ["Kernels.DelegateParameterReassignedKernel"],
            ["1", "2", "20", "30"]);
    }

    /// <summary>
    /// A lambda captures a view of global memory, which closures may capture
    /// unlike views of shared or local memory.
    /// </summary>
    [SkippableFact]
    public async Task ClosureCapturesView_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/ClosureCapturesView.cs",
            ["Kernels.ClosureCapturesViewKernel"],
            ["30", "20", "10", "0"]);
    }
}
