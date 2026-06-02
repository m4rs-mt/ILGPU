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
}
