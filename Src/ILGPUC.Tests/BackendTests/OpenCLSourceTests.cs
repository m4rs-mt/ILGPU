// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: OpenCLSourceTests.cs
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
/// Checks of generated OpenCL C source for constructs the OpenCL C language
/// forbids, which only native compilation would otherwise reveal.
/// </summary>
public sealed class OpenCLSourceTests
{
    /// <summary>
    /// OpenCL C forbids bool kernel arguments and bool has no defined size in
    /// structs: both must be emitted as one-byte values.
    /// </summary>
    [Fact]
    public void BoolArguments_AreNotEmittedAsBool()
    {
        var kernel = typeof(StructureKernels)
            .GetMethod(nameof(StructureKernels.BoolArgumentKernel))!;
        using var helper = new CompilationHelper(BackendType.OpenCL);
        var source = helper.GenerateBackendCode(kernel, BackendType.OpenCL).SourceCode;

        var signature = Regex.Match(source, @"kernel\s+void\s+\w+\s*\(([^)]*)\)");
        Assert.True(signature.Success, "Kernel signature not found");
        Assert.DoesNotMatch(@"\bbool\b", signature.Groups[1].Value);
        Assert.DoesNotMatch(@"\bbool\s+Field\d+\s*;", source);
    }

    /// <summary>
    /// An address-space keyword in front of a pointer type qualifies the
    /// element type of the pointer: a local pointer into global memory whose
    /// address is taken is a global pointer stored in private memory.
    /// </summary>
    [Fact]
    public void LocalPointerAddresses_QualifyThePointers()
    {
        var kernel = typeof(AddressSpaceKernels)
            .GetMethod(nameof(AddressSpaceKernels.LocalPointerAddressKernel))!;
        using var helper = new CompilationHelper(BackendType.OpenCL);
        var source = helper.GenerateBackendCode(kernel, BackendType.OpenCL).SourceCode;

        Assert.DoesNotMatch(
            @"\b(global|local|private|constant)\s+(global|local|private|constant)\b",
            source);
        Assert.Matches(@"global int\* private \w+_storage\[1\];", source);
        Assert.Matches(@"global int\* private\* \w+ = \w+_storage;", source);
        Assert.Matches(@"\(global void\* private\*\)", source);
    }

    /// <summary>
    /// A local array of pointers into global memory is declared in private
    /// memory and passed to the helper method using it as a pointer to its
    /// global pointers.
    /// </summary>
    [Fact]
    public void LocalPointerArrays_QualifyThePointers()
    {
        var kernel = typeof(AddressSpaceKernels)
            .GetMethod(nameof(AddressSpaceKernels.LocalPointerArrayKernel))!;
        using var helper = new CompilationHelper(BackendType.OpenCL);
        var source = helper.GenerateBackendCode(kernel, BackendType.OpenCL).SourceCode;

        Assert.DoesNotMatch(
            @"\b(global|local|private|constant)\s+(global|local|private|constant)\b",
            source);
        Assert.Matches(@"global void\* private \w+\[2\] = \{0\}", source);
        Assert.Matches(
            @"\(global int\* \w+, int \w+, global void\* private\* \w+\)",
            source);
    }
}
