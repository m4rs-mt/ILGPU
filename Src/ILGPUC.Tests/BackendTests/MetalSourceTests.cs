// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalSourceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Metal;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System;
using System.CommandLine;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Checks that the Metal backend rejects what Metal cannot express with a
/// clear compile-time error instead of emitting invalid source, that it
/// selects precise or fast math and fusion as requested and that it keeps
/// thread storage away from address 0.
/// </summary>
public sealed class MetalSourceTests
{
    /// <summary>
    /// Computes in double precision on float buffers.
    /// </summary>
    internal static void DoubleArithmeticKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data)
    {
        double value = data[index];
        data[index] = (float)(value * 0.5 + 1.0);
    }

    /// <summary>
    /// Metal has no 64-bit floats.
    /// </summary>
    [Fact]
    public void Double_ReportsError()
    {
        var kernel = typeof(MetalSourceTests)
            .GetMethod(
                nameof(DoubleArithmeticKernel),
                BindingFlags.NonPublic | BindingFlags.Static)!;
        using var helper = new CompilationHelper(BackendType.Metal);

        var exception = Assert.ThrowsAny<Exception>(
            () => helper.GenerateBackendCode(kernel, BackendType.Metal));
        Assert.Contains("64-bit floating-point", Flatten(exception));
    }

    /// <summary>
    /// Metal has no 64-bit atomics.
    /// </summary>
    [Fact]
    public void Int64Atomics_ReportError()
    {
        var kernel = typeof(AtomicKernels)
            .GetMethod(nameof(AtomicKernels.AtomicInt64Kernel))!;
        using var helper = new CompilationHelper(BackendType.Metal);

        var exception = Assert.ThrowsAny<Exception>(
            () => helper.GenerateBackendCode(kernel, BackendType.Metal));
        Assert.Contains("not supported by Metal", Flatten(exception));
    }

    /// <summary>
    /// Divides by a constant and subtracts a product in half precision.
    /// </summary>
    internal static void HalfDivisionKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> data)
    {
        data[index] = data[index] / (Half)3.0f - data[index + 1] * data[index + 2];
    }

    private static readonly MethodInfo HalfDivision = typeof(MetalSourceTests)
        .GetMethod(
            nameof(HalfDivisionKernel),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Turning fusion off disables multiply-add fusion and divides
    /// half values by constants through the correctly rounded division helper
    /// unless a fast math mode is requested; by default, the native division
    /// and multiply-adds are kept.
    /// </summary>
    [Theory]
    [InlineData(FusedMultiplyAddMode.Fast, MathMode.Default, false)]
    [InlineData(FusedMultiplyAddMode.Fast, MathMode.Fast, false)]
    [InlineData(FusedMultiplyAddMode.Off, MathMode.Default, true)]
    [InlineData(FusedMultiplyAddMode.Off, MathMode.Fast, false)]
    public void HalfDivision_EmitsPreciseMath(
        FusedMultiplyAddMode fusedMultiplyAdd,
        MathMode mathMode,
        bool precise)
    {
        using var helper = new CompilationHelper(BackendType.Metal);
        var source = helper.GenerateBackendCode(
            HalfDivision,
            BackendType.Metal,
            new CompilationProperties(
                MathMode: mathMode,
                FusedMultiplyAddMode: fusedMultiplyAdd)).SourceCode;

        Assert.Equal(
            fusedMultiplyAdd == FusedMultiplyAddMode.Off,
            source.Contains("#pragma STDC FP_CONTRACT OFF"));
        // The source header always defines the helper
        Assert.Equal(precise ? 2 : 1, source.Split("ilgpu_div_half(").Length - 1);
        Assert.Equal(
            precise,
            source.Contains("as_type<float>(0x3EAAAAABu)"));
    }

    /// <summary>
    /// The Metal compiler runs with fast math disabled unless a fast math mode
    /// is requested, and so does the runtime compilation of embedded source.
    /// Turning fusion off disables it in the Metal compiler as well.
    /// </summary>
    [Theory]
    [InlineData(MathMode.Default, FusedMultiplyAddMode.Fast, "-fno-fast-math", false)]
    [InlineData(
        MathMode.Default,
        FusedMultiplyAddMode.Off,
        "-fno-fast-math -ffp-contract=off",
        false)]
    [InlineData(MathMode.Fast, FusedMultiplyAddMode.Fast, "-ffast-math", true)]
    [InlineData(
        MathMode.Fast,
        FusedMultiplyAddMode.Off,
        "-ffast-math -ffp-contract=off",
        true)]
    [InlineData(MathMode.Fast32BitOnly, FusedMultiplyAddMode.Fast, "-ffast-math", true)]
    public async Task MathMode_SelectsCompileOptions(
        MathMode mathMode,
        FusedMultiplyAddMode fusedMultiplyAdd,
        string flags,
        bool fastMath)
    {
        var properties = new CompilationProperties(
            MathMode: mathMode,
            FusedMultiplyAddMode: fusedMultiplyAdd);
        using var helper = new CompilationHelper(BackendType.Metal);
        var source = helper.GenerateBackendCode(
            HalfDivision,
            BackendType.Metal,
            properties);

        var manager = new RecordingCompilerManager();
        var backend = CompilationHelper.CreateBackend(BackendType.Metal);
        await backend.CompileSourceAsync(source, manager, properties);
        Assert.Equal(flags, manager.Request?.ExtraFlags);

        var (_, wrapper) = helper.GenerateLauncher(
            HalfDivision,
            BackendType.Metal,
            props: properties);
        Assert.Equal(fastMath, wrapper.Contains("fastMath: true"));
    }

    /// <summary>
    /// Holds four integers in thread memory.
    /// </summary>
    internal unsafe struct LocalBuffer
    {
        public fixed int Data[4];
    }

    /// <summary>
    /// Stores into a local array and into a fixed buffer of a local.
    /// </summary>
    internal static unsafe void LocalStorageKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var values = new int[4];
        values[index & 3] = data[index];
        LocalBuffer buffer;
        buffer.Data[index & 3] = values[(index + 1) & 3];
        data[index] = buffer.Data[(index + 2) & 3];
    }

    private static readonly MethodInfo LocalStorage = typeof(MetalSourceTests)
        .GetMethod(
            nameof(LocalStorageKernel),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Some Metal devices place the first thread-private object of a kernel at
    /// address 0, where a local array or local compares equal to null. Local
    /// arrays and allocations start one element into a longer array instead,
    /// unless null-safe locals are turned off, which the compiled kernel then
    /// tells the runtime.
    /// </summary>
    [Theory]
    [InlineData(OptimizationLevel.O0, true)]
    [InlineData(OptimizationLevel.O1, true)]
    [InlineData(OptimizationLevel.O2, true)]
    [InlineData(OptimizationLevel.O0, false)]
    [InlineData(OptimizationLevel.O2, false)]
    public void LocalStorage_StartsAfterAddressZero(
        OptimizationLevel level,
        bool nullSafeLocals)
    {
        var properties = new CompilationProperties(
            OptimizationLevel: level,
            EnableMetalNullSafeLocals: nullSafeLocals);
        using var helper = new CompilationHelper(BackendType.Metal);
        var source = helper.GenerateBackendCode(
            LocalStorage,
            BackendType.Metal,
            properties).SourceCode;

        if (nullSafeLocals)
        {
            Assert.Matches(@"thread int (\w+)_storage\[5\] = \{0\};", source);
            Assert.Matches(@"thread int\* (\w+) = \1_storage \+ 1;", source);
            Assert.Matches(@"thread (\w+) (\w+)_storage_base\[2\] = \{\};", source);
            Assert.Matches(@"thread \w+\* (\w+)_storage = \1_storage_base \+ 1;", source);
        }
        else
        {
            Assert.Matches(@"thread int \w+\[4\] = \{0\}\s*;", source);
            Assert.Matches(@"thread \w+ \w+_storage\[1\];", source);
            Assert.DoesNotContain("_storage_base", source);
            Assert.DoesNotContain(" + 1;", source);
        }

        var (_, wrapper) = helper.GenerateLauncher(
            LocalStorage,
            BackendType.Metal,
            props: properties);
        Assert.Equal(!nullSafeLocals, wrapper.Contains("nullSafeLocals: false"));
    }

    /// <summary>
    /// Null-safe locals are the default of the compilation properties and of the
    /// command line, which turns them off with false.
    /// </summary>
    [Theory]
    [InlineData(new string[0], true)]
    [InlineData(new[] { "--metal-null-safe-locals" }, true)]
    [InlineData(new[] { "--metal-null-safe-locals", "false" }, false)]
    [InlineData(new[] { "--metal-null-safe-locals=false" }, false)]
    public void CommandLine_SelectsNullSafeLocals(string[] args, bool expected)
    {
        Assert.True(new CompilationProperties().EnableMetalNullSafeLocals);

        var command = new Command("compile");
        var options = new Program.CompilationPropertiesOptions(command);
        var result = command.Parse(args);

        Assert.Empty(result.Errors);
        Assert.Equal(
            expected,
            options.GetProperties(result).EnableMetalNullSafeLocals);
    }

    /// <summary>
    /// The Metal runtime warns when it loads a kernel without null-safe locals
    /// on the Apple Paravirtual device of macOS virtual machines, which places
    /// the first thread-private object of a kernel at address 0.
    /// </summary>
    [Theory]
    [InlineData(false, "Apple Paravirtual device", true)]
    [InlineData(true, "Apple Paravirtual device", false)]
    [InlineData(false, "Apple M4 Max", false)]
    public void NullSafeLocals_WarnsOnParavirtualDevice(
        bool nullSafeLocals,
        string deviceName,
        bool warns)
    {
        var kernel = new MetalCompiledKernel(
            Guid.NewGuid(),
            "LocalStorageKernel",
            CompiledKernelType.Auto,
            CompiledKernelSharedMemoryMode.Static,
            MetalTargetOS.macOS,
            MetalGPUFamily.Apple7,
            nullSafeLocals: nullSafeLocals);
        var getWarning = typeof(MetalKernel).GetMethod(
            "GetNullSafeLocalsWarning",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var warning = (string?)getWarning.Invoke(null, [kernel, deviceName]);
        Assert.Equal(warns, warning is not null);
        if (warning is not null)
        {
            Assert.Contains("'LocalStorageKernel'", warning);
            Assert.Contains("--metal-null-safe-locals", warning);
        }
    }

    private static string Flatten(Exception exception)
    {
        var message = string.Empty;
        for (Exception? current = exception; current is not null;
            current = current.InnerException)
        {
            message += current.Message + Environment.NewLine;
        }
        return message;
    }
}
