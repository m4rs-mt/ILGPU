// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CpuSourceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System.Text.RegularExpressions;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Checks of generated CPU C# source for constructs C# rejects, which only the
/// compilation of an execution test program would otherwise reveal.
/// </summary>
public sealed class CpuSourceTests
{
    private static readonly Regex ConstantStore =
        new(@"\.ScatterFill\([^,]+, 0L?, activeMask\);");

    /// <summary>
    /// The kernels reading members of null views and the number of their
    /// stores of a constant zero.
    /// </summary>
    public static TheoryData<string, int, OptimizationLevel> EmptyViewCases
    {
        get
        {
            var data = new TheoryData<string, int, OptimizationLevel>();
            foreach (var opt in new[]
                { OptimizationLevel.O0, OptimizationLevel.O1, OptimizationLevel.O2 })
            {
                data.Add(nameof(ArrayViewKernels.EmptyViewIntLengthKernel), 1, opt);
                data.Add(nameof(ArrayViewKernels.EmptyViewMembersKernel), 5, opt);
                data.Add(nameof(ArrayViewKernels.DefaultViewMembersKernel), 5, opt);
                data.Add(nameof(ArrayViewKernels.EmptyViewElementKernel), 0, opt);
            }
            return data;
        }
    }

    /// <summary>
    /// The length of a null view (the empty view or a default view) is the
    /// constant zero, and the remaining members of a null view are accessed on
    /// a typed default: C# has no member access on the untyped default
    /// literal (CS8716).
    /// </summary>
    [Theory]
    [MemberData(nameof(EmptyViewCases))]
    public void EmptyViewMembers_AreConstantsOrTypedDefaults(
        string kernelName,
        int constantStores,
        OptimizationLevel opt)
    {
        var kernel = typeof(ArrayViewKernels).GetMethod(kernelName)!;
        using var helper = new CompilationHelper(BackendType.CPU);
        var source = helper.GenerateBackendCode(
            kernel,
            BackendType.CPU,
            new CompilationProperties(OptimizationLevel: opt)).SourceCode;

        Assert.DoesNotContain("default.", source);
        Assert.Equal(constantStores, ConstantStore.Matches(source).Count);
    }
}
