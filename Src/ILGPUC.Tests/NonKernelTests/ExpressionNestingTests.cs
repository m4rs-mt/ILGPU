// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ExpressionNestingTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using RoslynLocation = Microsoft.CodeAnalysis.Location;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that expressions nested too deeply for the stack of the compiling thread
/// are reported as a located error instead of overflowing the stack, and that the
/// ilgpuc tool compiles deeply nested expressions on its large thread stacks.
/// </summary>
public sealed class ExpressionNestingTests
{
    #region Kernels

    // Kernels defined here (not in Kernels/) to avoid auto-discovery by
    // KernelRegistry. Every helper doubles the number of steps of the one it
    // calls, and inlining turns the steps into a single chain of four operations
    // per step.
    internal static class Kernels
    {
        private static int Step(int x) => (x ^ (x >> 3)) * 31 + 7; // Step

        private static int Steps2(int x) => Step(Step(x));
        private static int Steps4(int x) => Steps2(Steps2(x));
        private static int Steps8(int x) => Steps4(Steps4(x));
        private static int Steps16(int x) => Steps8(Steps8(x));
        private static int Steps32(int x) => Steps16(Steps16(x));
        private static int Steps64(int x) => Steps32(Steps32(x));
        private static int Steps128(int x) => Steps64(Steps64(x));
        private static int Steps256(int x) => Steps128(Steps128(x));
        private static int Steps512(int x) => Steps256(Steps256(x));
        private static int Steps1024(int x) => Steps512(Steps512(x));
        private static int Steps2048(int x) => Steps1024(Steps1024(x));

        /// <summary>
        /// A chain of 10,240 operations.
        /// </summary>
        public static void DeepChainKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Steps2048(Steps512(data[index]));
        }

        /// <summary>
        /// A chain of 3,072 operations.
        /// </summary>
        public static void ChainKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Steps512(Steps256(data[index]));
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// The stack size of the thread compiling the deep chain: the default stack
    /// size of threads on Windows, the smallest of all supported platforms.
    /// </summary>
    private const int StackSize = 1 << 20;

    /// <summary>
    /// The location of the launch site errors without a source location are
    /// reported at.
    /// </summary>
    private static readonly RoslynLocation LaunchSite = RoslynLocation.Create(
        "/src/Launch.cs",
        default,
        new LinePositionSpan(new LinePosition(4, 8), new LinePosition(4, 20)));

    private static MethodInfo GetKernel(string name) =>
        typeof(Kernels).GetMethod(name, BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Kernel method '{name}' not found.");

    /// <summary>
    /// Compiles the given kernel for the CPU backend on a thread with a stack of
    /// <see cref="StackSize"/> bytes and returns the error it fails with.
    /// </summary>
    private static Exception? CompileOnSmallStack(string kernelName)
    {
        Exception? error = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    using var helper = new CompilationHelper(BackendType.CPU);
                    helper.GenerateBackendCode(GetKernel(kernelName), BackendType.CPU);
                }
                catch (Exception exception)
                {
                    error = exception;
                }
            },
            StackSize);
        thread.Start();
        thread.Join();
        return error;
    }

    /// <summary>
    /// Returns the line of this file marked with the given comment.
    /// </summary>
    private static int GetMarkedLine(
        string marker,
        [CallerFilePath] string sourcePath = "")
    {
        var lines = File.ReadAllLines(sourcePath);
        int line = Array.FindIndex(
            lines,
            l => l.EndsWith("// " + marker, StringComparison.Ordinal)) + 1;
        Assert.True(line > 0, $"Marker '{marker}' not found");
        return line;
    }

    #endregion

    [Fact]
    public void DeepExpression_ReportsLocatedNestingError()
    {
        var exception = Assert.IsType<KernelCompilationException>(
            CompileOnSmallStack(nameof(Kernels.DeepChainKernel)));

        Assert.Equal("ILGPU1200", exception.Id);
        Assert.Equal(
            "An expression in method 'DeepChainKernel' is nested too deeply to be " +
            "compiled. Compute long chains of operations in a loop instead of " +
            "unrolling them, or split them across methods marked with " +
            "MethodImplOptions.NoInlining.",
            exception.Message);
        var location = Assert.IsAssignableFrom<FileLocation>(exception.Location);
        Assert.Equal(
            nameof(ExpressionNestingTests) + ".cs",
            Path.GetFileName(location.FileName));
        Assert.Equal(GetMarkedLine("Step"), location.StartLine);

        // ilgpuc builds report it at the location of the expression
        Assert.True(KernelCompilationErrors.TryCreateDiagnostic(
            exception,
            LaunchSite,
            out var diagnostic));
        Assert.Equal("ILGPU1200", diagnostic!.Id);
        Assert.Contains("is nested too deeply", diagnostic.Message);
        var span = diagnostic.Location.GetLineSpan();
        Assert.Equal(location.FileName, span.Path);
        Assert.Equal(location.StartLine, span.StartLinePosition.Line + 1);
    }

    [Fact]
    public async Task DeepExpression_IsCompiledByTheTool()
    {
        var outputDir = Directory.CreateTempSubdirectory("ILGPUC").FullName;
        try
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in new[]
            {
                IntegrationProjectFixture.IlgpucDllPath,
                "compile",
                "-i", typeof(Kernels).Assembly.Location,
                "-k", typeof(Kernels).FullName + "." + nameof(Kernels.ChainKernel),
                "-o", outputDir,
                "-b", nameof(BackendType.CPU),
                // Compiles on thread-pool threads
                "--compile",
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(
                process.ExitCode == 0,
                $"ilgpuc exited with {process.ExitCode}:\n{await output}\n{await error}");
            Assert.NotEmpty(Directory.GetFiles(outputDir, "*.cs"));
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }
}
