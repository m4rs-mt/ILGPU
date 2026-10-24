// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FusedMultiplyAddSourceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System;
using System.CommandLine;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Checks that every backend allows multiply-add fusion by default and
/// disables it in the generated source, the native compiler options and the
/// runtime compilation of embedded source if fusion is turned off.
/// </summary>
public sealed class FusedMultiplyAddSourceTests
{
    /// <summary>
    /// Subtracts two products, which native compilers fuse into a multiply-add.
    /// </summary>
    internal static void FusionKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data)
    {
        data[index] = data[index] * data[index + 1] - data[index + 2] * data[index + 3];
    }

    private static readonly MethodInfo Fusion = typeof(FusedMultiplyAddSourceTests)
        .GetMethod(
            nameof(FusionKernel),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string NormalizeNames(string source) =>
        Regex.Replace(source, @"(?<=_)\d+", "N");

    private static CompilationProperties GetProperties(
        FusedMultiplyAddMode fusedMultiplyAdd) =>
        new(FusedMultiplyAddMode: fusedMultiplyAdd);

    /// <summary>
    /// Returns the flags of the native compile request of the given backend.
    /// </summary>
    private static async Task<string> GetCompilerFlagsAsync(
        BackendType backendType,
        FusedMultiplyAddMode fusedMultiplyAdd)
    {
        var properties = GetProperties(fusedMultiplyAdd);
        using var helper = new CompilationHelper(backendType);
        var source = helper.GenerateBackendCode(Fusion, backendType, properties);

        var manager = new RecordingCompilerManager();
        var backend = CompilationHelper.CreateBackend(backendType);
        await backend.CompileSourceAsync(source, manager, properties);
        Assert.NotNull(manager.Request);
        return manager.Request.ExtraFlags ?? string.Empty;
    }

    /// <summary>
    /// Fusion is the default of the compilation properties and of the
    /// command line.
    /// </summary>
    [Fact]
    public void Default_AllowsFusion()
    {
        Assert.Equal(
            FusedMultiplyAddMode.Fast,
            new CompilationProperties().FusedMultiplyAddMode);

        var command = new Command("compile");
        var options = new Program.CompilationPropertiesOptions(command);
        Assert.Equal(
            FusedMultiplyAddMode.Fast,
            options.GetProperties(command.Parse([])).FusedMultiplyAddMode);
    }

    /// <summary>
    /// The --fma option selects the fusion mode in any case.
    /// </summary>
    [Theory]
    [InlineData("fast", FusedMultiplyAddMode.Fast)]
    [InlineData("off", FusedMultiplyAddMode.Off)]
    [InlineData("Off", FusedMultiplyAddMode.Off)]
    public void CommandLine_SelectsFusion(
        string value,
        FusedMultiplyAddMode expected)
    {
        var command = new Command("compile");
        var options = new Program.CompilationPropertiesOptions(command);
        var result = command.Parse(["--fma", value]);

        Assert.Empty(result.Errors);
        Assert.Equal(expected, options.GetProperties(result).FusedMultiplyAddMode);
    }

    /// <summary>
    /// nvcc fuses multiply-adds unless --fmad=false is passed.
    /// </summary>
    [Theory]
    [InlineData(FusedMultiplyAddMode.Fast, false)]
    [InlineData(FusedMultiplyAddMode.Off, true)]
    public async Task Cuda_PassesFmadFlag(
        FusedMultiplyAddMode fusedMultiplyAdd,
        bool disabled)
    {
        var flags = await GetCompilerFlagsAsync(BackendType.Cuda, fusedMultiplyAdd);
        Assert.Equal(disabled, flags.Contains("--fmad=false"));
        Assert.DoesNotContain("--fmad=true", flags);
    }

    /// <summary>
    /// hipcc fuses multiply-adds unless -ffp-contract=off is passed.
    /// </summary>
    [Theory]
    [InlineData(FusedMultiplyAddMode.Fast, false)]
    [InlineData(FusedMultiplyAddMode.Off, true)]
    public async Task ROCm_PassesFusionFlag(
        FusedMultiplyAddMode fusedMultiplyAdd,
        bool disabled)
    {
        var flags = await GetCompilerFlagsAsync(BackendType.ROCm, fusedMultiplyAdd);
        Assert.Equal(disabled, flags.Contains("-ffp-contract=off"));
    }

    /// <summary>
    /// The Metal compiler fuses multiply-adds unless -ffp-contract=off is
    /// passed.
    /// </summary>
    [Theory]
    [InlineData(FusedMultiplyAddMode.Fast, false)]
    [InlineData(FusedMultiplyAddMode.Off, true)]
    public async Task Metal_PassesFusionFlag(
        FusedMultiplyAddMode fusedMultiplyAdd,
        bool disabled)
    {
        var flags = await GetCompilerFlagsAsync(BackendType.Metal, fusedMultiplyAdd);
        Assert.Equal(disabled, flags.Contains("-ffp-contract=off"));
    }

    /// <summary>
    /// OpenCL and Metal sources disable fusion with their pragmas, which
    /// apply to the offline compilers and to the source the runtime compiles
    /// if no binary is embedded. CUDA and ROCm sources need no pragma, as
    /// their native compilers receive the flags.
    /// </summary>
    [Theory]
    [InlineData(BackendType.OpenCL, "#pragma OPENCL FP_CONTRACT OFF")]
    [InlineData(BackendType.Metal, "#pragma STDC FP_CONTRACT OFF")]
    public void Source_EmitsFusionPragma(BackendType backendType, string pragma)
    {
        using var helper = new CompilationHelper(backendType);
        foreach (var mode in Enum.GetValues<FusedMultiplyAddMode>())
        {
            bool disabled = mode == FusedMultiplyAddMode.Off;
            var source = helper.GenerateBackendCode(
                Fusion,
                backendType,
                GetProperties(mode)).SourceCode;
            Assert.Equal(disabled, source.Contains(pragma));
            Assert.DoesNotContain("FP_CONTRACT ON", source);

            // Without a binary, the wrapper embeds the source for the runtime
            var (_, wrapper) = helper.GenerateLauncher(
                Fusion,
                backendType,
                props: GetProperties(mode));
            Assert.Contains("KernelSource", wrapper);
            Assert.Equal(disabled, wrapper.Contains(pragma));
        }
    }

    /// <summary>
    /// CUDA and ROCm sources are the same in both modes (up to the generated
    /// names), and so are CPU kernels, whose C# multiplications and additions
    /// the .NET JIT never fuses.
    /// </summary>
    [Theory]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.CPU)]
    public void Source_IsIndependentOfFusion(BackendType backendType)
    {
        using var helper = new CompilationHelper(backendType);
        var fast = helper.GenerateBackendCode(
            Fusion,
            backendType,
            GetProperties(FusedMultiplyAddMode.Fast)).SourceCode;
        var off = helper.GenerateBackendCode(
            Fusion,
            backendType,
            GetProperties(FusedMultiplyAddMode.Off)).SourceCode;
        Assert.Equal(NormalizeNames(fast), NormalizeNames(off));
        Assert.DoesNotContain("FP_CONTRACT", off);
    }
}
