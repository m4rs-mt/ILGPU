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
using ILGPUC.Compilers;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

    /// <summary>
    /// OpenCL C forbids kernel arguments pointing to pointers: views of
    /// pointers are passed as byte pointers and converted in the kernel.
    /// </summary>
    [Fact]
    public void PointerViewArguments_DoNotPointToPointers()
    {
        var kernel = typeof(AddressSpaceKernels)
            .GetMethod(nameof(AddressSpaceKernels.PointerViewKernel))!;
        using var helper = new CompilationHelper(BackendType.OpenCL);
        var source = helper.GenerateBackendCode(kernel, BackendType.OpenCL).SourceCode;

        var signature = Regex.Match(source, @"kernel\s+void\s+\w+\s*\(([^)]*)\)");
        Assert.True(signature.Success, "Kernel signature not found");
        Assert.DoesNotMatch(@"\*\s*(global\s*)?\*", signature.Groups[1].Value);
        Assert.Matches(
            @"global int\* global\* (\w+) = \(global int\* global\*\)" +
            @"\(\1_buffer \+ \1_offset\);",
            source);
        Assert.Matches(
            @"global void\* global\* (\w+) = \(global void\* global\*\)" +
            @"\(\1_buffer \+ \1_offset\);",
            source);
    }

    /// <summary>
    /// Integers converted to pointers, such as null pointers, are cast to the
    /// OpenCL C pointer type.
    /// </summary>
    [Fact]
    public void IntegersAsPointers_AreCastToPointerTypes()
    {
        var kernel = typeof(AddressSpaceKernels)
            .GetMethod(nameof(AddressSpaceKernels.NullPointerKernel))!;
        using var helper = new CompilationHelper(BackendType.OpenCL);
        var source = helper.GenerateBackendCode(kernel, BackendType.OpenCL).SourceCode;

        Assert.DoesNotContain("nint", source);
        Assert.Contains("((global int*)0)", source);
    }

    /// <summary>
    /// Null pointers, such as the initial value of a local pointer, are zeros
    /// cast to the OpenCL C pointer type, as OpenCL C compilers reject empty
    /// initializers of scalars.
    /// </summary>
    [Fact]
    public void NullPointers_AreZerosCastToPointerTypes()
    {
        var kernel = typeof(AddressSpaceKernels)
            .GetMethod(nameof(AddressSpaceKernels.LocalPointerAddressKernel))!;
        using var helper = new CompilationHelper(BackendType.OpenCL);
        var source = helper.GenerateBackendCode(kernel, BackendType.OpenCL).SourceCode;

        Assert.DoesNotContain("*){})", source);
        Assert.Contains("((global int*)0)", source);
    }

    private const string Fp16Pragma =
        "#pragma OPENCL EXTENSION cl_khr_fp16 : enable";

    /// <summary>
    /// The kernels using half: half values, and a structure with half fields
    /// that is only copied, so that no value of the half type itself appears.
    /// </summary>
    public static TheoryData<Type, string> HalfKernels => new()
    {
        { typeof(FloatArithmeticKernels),
            nameof(FloatArithmeticKernels.HalfAbsIsNaNKernel) },
        { typeof(HalfVectorKernels),
            nameof(HalfVectorKernels.CopyFloat16x2Kernel) },
    };

    /// <summary>
    /// OpenCL C disallows half variables and arithmetic unless cl_khr_fp16 is
    /// enabled before their first use, which is only done for modules using
    /// half.
    /// </summary>
    [Theory]
    [MemberData(nameof(HalfKernels))]
    public void HalfKernels_EnableFp16(Type kernelClass, string kernelName)
    {
        var source = GenerateSource(kernelClass, kernelName);
        var half = Regex.Match(source, @"\bhalf\b");
        Assert.True(half.Success, "No half type in the source");
        int pragma = source.IndexOf(Fp16Pragma, StringComparison.Ordinal);
        Assert.InRange(pragma, 0, half.Index);
    }

    [Fact]
    public void KernelsWithoutHalf_DoNotEnableFp16()
    {
        var source = GenerateSource(
            typeof(StructureKernels),
            nameof(StructureKernels.BoolArgumentKernel));
        Assert.DoesNotMatch(@"\bhalf\b", source);
        Assert.DoesNotContain("cl_khr_fp16", source);
    }

    /// <summary>
    /// clang rejects half variables in OpenCL C without cl_khr_fp16, whereas
    /// ocloc accepts them, so the generated source is parsed by clang for
    /// SPIR-V. Skipped where clang or its spirv64 target is unavailable, or
    /// where clang does not parse a trivial kernel in time.
    /// </summary>
    [SkippableFact]
    public async Task HalfKernel_ParsesWithClang()
    {
        var clang = new CompilerOptions().ClangPath;
        Skip.IfNot(File.Exists(clang), $"{clang} is not available");
        var probe = await ParseWithClangAsync(
            clang,
            "kernel void Probe(global int* data) { *data = 1; }");
        Skip.IfNot(
            probe.ExitCode == 0,
            $"{clang} cannot parse OpenCL C for spirv64: {probe.StdErr}");

        var source = GenerateSource(
            typeof(FloatArithmeticKernels),
            nameof(FloatArithmeticKernels.HalfAbsIsNaNKernel));
        var result = await ParseWithClangAsync(clang, source);
        Assert.True(
            result.ExitCode == 0,
            $"clang failed on the source: {result.StdErr}");
    }

    private static string GenerateSource(Type kernelClass, string kernelName)
    {
        var kernel = kernelClass.GetMethod(kernelName)!;
        using var helper = new CompilationHelper(BackendType.OpenCL);
        return helper.GenerateBackendCode(kernel, BackendType.OpenCL).SourceCode;
    }

    /// <summary>
    /// Checks the syntax and semantics of OpenCL C source with clang without
    /// generating code. A clang that does not finish in time is killed and
    /// reported as failed with exit code -1.
    /// </summary>
    private static async Task<(int ExitCode, string StdErr)> ParseWithClangAsync(
        string clang,
        string source)
    {
        var startInfo = new ProcessStartInfo(clang)
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
        {
            "-target", "spirv64-unknown-unknown", "-cl-std=CL2.0",
            "-x", "cl", "-fsyntax-only", "-",
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var timeout = new CancellationTokenSource(ClangTimeout);
        using var process = Process.Start(startInfo)!;
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.StandardInput.WriteAsync(source.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            await stdout;
            return (process.ExitCode, await stderr);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            // A hanging clang is killed instead of outliving the test run.
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            return (-1, $"clang did not finish within {ClangTimeout.TotalSeconds} s");
        }
    }

    private static readonly TimeSpan ClangTimeout = TimeSpan.FromSeconds(60);
}
