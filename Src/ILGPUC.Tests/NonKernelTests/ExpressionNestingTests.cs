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
using ILGPUC.Tests.Framework;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that the ilgpuc tool compiles deeply nested expressions on its large
/// thread stacks.
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
        private static int Step(int x) => (x ^ (x >> 3)) * 31 + 7;

        private static int Steps2(int x) => Step(Step(x));
        private static int Steps4(int x) => Steps2(Steps2(x));
        private static int Steps8(int x) => Steps4(Steps4(x));
        private static int Steps16(int x) => Steps8(Steps8(x));
        private static int Steps32(int x) => Steps16(Steps16(x));
        private static int Steps64(int x) => Steps32(Steps32(x));
        private static int Steps128(int x) => Steps64(Steps64(x));
        private static int Steps256(int x) => Steps128(Steps128(x));
        private static int Steps512(int x) => Steps256(Steps256(x));

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
