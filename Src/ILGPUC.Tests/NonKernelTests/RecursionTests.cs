// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RecursionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.Transformations;
using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using Microsoft.CodeAnalysis.Text;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using IRModule = ILGPUC.IR.ModuleValues.Module;
using RoslynLocation = Microsoft.CodeAnalysis.Location;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that methods calling themselves, directly or through other methods, are
/// compiled as recursive functions instead of being inlined, with a warning
/// (ILGPU1210).
/// </summary>
public sealed class RecursionTests
{
    #region Kernels

    // Kernels defined here (not in Kernels/) to avoid auto-discovery by
    // KernelRegistry. Every reported line is marked with a comment naming it,
    // which the tests look up to check the reported location.
    private static class Kernels
    {
        private static int Factorial(int n) =>
            n <= 1 ? 1 : n * Factorial(n - 1); // Factorial

        public static void FactorialKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Factorial(data[index]);
        }

        private static bool IsEven(int n) => n == 0 || IsOdd(n - 1);

        private static bool IsOdd(int n) =>
            n != 0 && IsEven(n - 1); // IsOdd

        public static void MutualRecursionKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = IsEven(data[index]) ? 1 : 0;
        }

        public static void MutualRecursionFromOddKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = IsOdd(data[index]) ? 1 : 0;
        }

        private static int GenericFactorial<T>(int n)
            where T : struct =>
            n <= 1 ? 1 : n * GenericFactorial<T>(n - 1); // GenericFactorial

        public static void GenericRecursionKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = GenericFactorial<int>(data[index]) +
                GenericFactorial<float>(data[index]);
        }

        private static int Twice(int x) => 2 * x;

        private static int PowerOfTwo(int n) => n <= 0 ? 1 : Twice(PowerOfTwo(n - 1));

        public static void PowerOfTwoKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = PowerOfTwo(data[index]);
        }

        private static int Square(int x) => x * x;

        public static void NonRecursiveKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Square(Square(data[index]));
        }
    }

    private static MethodInfo GetKernel(string name) =>
        typeof(Kernels).GetMethod(name, BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Kernel method '{name}' not found.");

    /// <summary>
    /// Compiles the given kernel for the CPU backend and returns the warnings
    /// about its code.
    /// </summary>
    private static async Task<KernelCompilationWarning[]> GetWarningsAsync(
        string kernelName)
    {
        var result = await new KernelCompiler().CompileKernelAsync(
            GetKernel(kernelName),
            CompilationHelper.CreateBackend(BackendType.CPU));
        return [.. result.Warnings];
    }

    /// <summary>
    /// Compiles the given kernel to a module after all global optimizations.
    /// </summary>
    private static IRModule Optimize(string kernelName)
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var properties = new CompilationProperties();
        var module = helper.CompileToModule(GetKernel(kernelName), properties);
        return properties.OptimizationLevel.CreateTransformer().Apply(
            properties,
            helper.TypeManager,
            module);
    }

    /// <summary>
    /// Asserts that the given location points at the line of this file marked
    /// with the given comment.
    /// </summary>
    private static void AssertLocation(
        Location location,
        string marker,
        [CallerFilePath] string sourcePath = "")
    {
        var fileLocation = Assert.IsAssignableFrom<FileLocation>(location);
        Assert.Equal(
            Path.GetFileName(sourcePath),
            Path.GetFileName(fileLocation.FileName));
        var lines = File.ReadAllLines(sourcePath);
        int line = Array.FindIndex(
            lines,
            l => l.EndsWith("// " + marker, StringComparison.Ordinal)) + 1;
        Assert.True(line > 0, $"Marker '{marker}' not found");
        Assert.Equal(line, fileLocation.StartLine);
    }

    #endregion

    #region Warnings

    [Fact]
    public async Task DirectRecursion_ReportsWarningAtTheRecursiveCall()
    {
        var warning = Assert.Single(
            await GetWarningsAsync(nameof(Kernels.FactorialKernel)));

        Assert.Equal("ILGPU1210", warning.Id);
        Assert.Equal(
            "Method 'Factorial' calls itself and is compiled as a recursive " +
            "function instead of being inlined. Deep recursion can exhaust the " +
            "stack of a thread.",
            warning.Message);
        AssertLocation(warning.Location, "Factorial");
    }

    [Fact]
    public async Task MutualRecursion_ReportsOneWarningForTheCycle()
    {
        var warning = Assert.Single(
            await GetWarningsAsync(nameof(Kernels.MutualRecursionKernel)));

        Assert.Equal("ILGPU1210", warning.Id);
        Assert.Equal(
            "Methods 'IsEven', 'IsOdd' call each other and are compiled as " +
            "recursive functions instead of being inlined. Deep recursion can " +
            "exhaust the stack of a thread.",
            warning.Message);
        AssertLocation(warning.Location, "IsOdd");
    }

    [Fact]
    public async Task MutualRecursion_EnteredAtEitherMethod_ReportsTheSameWarning()
    {
        var fromEven = Assert.Single(
            await GetWarningsAsync(nameof(Kernels.MutualRecursionKernel)));
        var fromOdd = Assert.Single(
            await GetWarningsAsync(nameof(Kernels.MutualRecursionFromOddKernel)));

        Assert.Equal(fromEven.ToString(), fromOdd.ToString());
        AssertLocation(fromOdd.Location, "IsOdd");
    }

    [Fact]
    public async Task GenericRecursion_ReportsTheSameWarningForEachInstantiation()
    {
        var warnings = await GetWarningsAsync(nameof(Kernels.GenericRecursionKernel));

        Assert.Equal(2, warnings.Length);
        Assert.All(warnings, warning =>
        {
            Assert.Equal("ILGPU1210", warning.Id);
            Assert.StartsWith(
                "Method 'GenericFactorial' calls itself",
                warning.Message,
                StringComparison.Ordinal);
            AssertLocation(warning.Location, "GenericFactorial");
        });
        Assert.Single(warnings.Select(warning => warning.ToString()).Distinct());
    }

    [Fact]
    public async Task NonRecursiveHelpers_ReportNoWarning() =>
        Assert.Empty(await GetWarningsAsync(nameof(Kernels.NonRecursiveKernel)));

    #endregion

    #region Inlining

    [Fact]
    public void RecursiveMethods_AreNotInlined()
    {
        var cycle = Assert.Single(CallCycle.FindAll(
            Optimize(nameof(Kernels.MutualRecursionKernel)).Methods.ToArray()));

        Assert.Equal("'IsEven', 'IsOdd'", cycle.FormatMethodNames());
    }

    [Fact]
    public void HelpersOfRecursiveMethods_AreInlined()
    {
        var module = Optimize(nameof(Kernels.PowerOfTwoKernel));

        var cycle = Assert.Single(CallCycle.FindAll(module.Methods.ToArray()));
        Assert.Equal("'PowerOfTwo'", cycle.FormatMethodNames());
        Assert.DoesNotContain(
            module.Methods.ToArray(),
            method => method.Source?.Name == "Twice");
    }

    #endregion

    #region Backends

    [Theory]
    [InlineData(BackendType.CPU)]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    public void BackendsWithRecursiveCalls_CompileRecursion(BackendType backend)
    {
        using var helper = new CompilationHelper(backend);
        helper.GenerateBackendCode(GetKernel(nameof(Kernels.FactorialKernel)), backend);
        helper.GenerateBackendCode(
            GetKernel(nameof(Kernels.MutualRecursionKernel)),
            backend);
    }

    #endregion

    #region Build Diagnostics

    /// <summary>
    /// The location of the launch site warnings without a source location are
    /// reported at.
    /// </summary>
    private static readonly RoslynLocation LaunchSite = RoslynLocation.Create(
        "/src/Launch.cs",
        default,
        new LinePositionSpan(new LinePosition(4, 8), new LinePosition(4, 20)));

    [Fact]
    public void Warning_IsReportedAtItsSourceLocation()
    {
        var warning = new KernelCompilationWarning(
            "ILGPU1210",
            "Message.",
            new FileLocation("/src/Kernels.cs", 9, 20, 12, 12));

        Assert.Equal(
            "/src/Kernels.cs(12,9): warning ILGPU1210: Message.",
            KernelCompilationErrors.CreateWarning(warning, LaunchSite).ToString());
    }

    [Fact]
    public void Warning_WithoutLocation_IsReportedAtLaunchSite()
    {
        var warning = new KernelCompilationWarning(
            "ILGPU1210",
            "Message.",
            Location.Unknown);

        Assert.Equal(
            "/src/Launch.cs(5,9): warning ILGPU1210: Message.",
            KernelCompilationErrors.CreateWarning(warning, LaunchSite).ToString());
        Assert.Equal(
            "ILGPUC: warning ILGPU1210: Message.",
            KernelCompilationErrors.CreateWarning(warning, null).ToString());
    }

    /// <summary>
    /// A program launching two kernels that call the same recursive helper.
    /// </summary>
    private const string BuildSource = """
        using System;
        using ILGPU;
        using ILGPU.Runtime;

        static class K
        {
            static int Factorial(int n) =>
                n <= 1 ? 1 : n * Factorial(n - 1); // recursion

            public static void First(Index1D index, ArrayView<int> view) =>
                view[index] = Factorial(index);

            public static void Second(Index1D index, ArrayView<int> view) =>
                view[index] = Factorial(index + 1);
        }

        static class Program
        {
            static void Main()
            {
                using var context = Context.Create(b => b.Default());
                using var accelerator = context.GetPreferredDevice(preferCPU: true)
                    .CreateAccelerator(context);
                var stream = accelerator.DefaultStream;
                using var buffer = stream.Allocate1D<int>(4);
                var view = buffer.View;
                stream.Launch((Index1D)4, index => K.First(index, view));
                stream.Launch((Index1D)4, index => K.Second(index, view));
                stream.Synchronize();
            }
        }
        """;

    /// <summary>
    /// Returns the line of <see cref="BuildSource"/> marked with
    /// <c>// recursion</c>.
    /// </summary>
    private static int RecursionLine =>
        Array.FindIndex(
            BuildSource.Split('\n'),
            l => l.TrimEnd().EndsWith("// recursion", StringComparison.Ordinal)) + 1;

    [Fact]
    public async Task Build_ReportsRecursionWarningOnce()
    {
        var compilation = RoslynCompiler.CreateCompilation(
            [BuildSource],
            "RecursionTest");
        var analysis = CompilationRewriter.Analyze(compilation);
        Assert.Empty(analysis.Diagnostics);

        var provider = await MainKernelProvider.CreateAsync(
            compilation,
            analysis,
            [
                CompilationHelper.CreateBackend(BackendType.CPU),
                CompilationHelper.CreateBackend(BackendType.Cuda),
            ],
            new KernelCompiler());

        var diagnostic = Assert.Single(provider.Diagnostics).ToString();
        Assert.StartsWith(
            $"source0.cs({RecursionLine},",
            diagnostic,
            StringComparison.Ordinal);
        Assert.Contains(
            "): warning ILGPU1210: Method 'Factorial' calls itself",
            diagnostic,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A program launching two kernels that enter the same call cycle at different
    /// methods.
    /// </summary>
    private const string MutualBuildSource = """
        using System;
        using ILGPU;
        using ILGPU.Runtime;

        static class K
        {
            static bool IsEven(int n) => n == 0 || IsOdd(n - 1);

            static bool IsOdd(int n) => n != 0 && IsEven(n - 1);

            public static void First(Index1D index, ArrayView<int> view) =>
                view[index] = IsEven(index) ? 1 : 0;

            public static void Second(Index1D index, ArrayView<int> view) =>
                view[index] = IsOdd(index) ? 1 : 0;
        }

        static class Program
        {
            static void Main()
            {
                using var context = Context.Create(b => b.Default());
                using var accelerator = context.GetPreferredDevice(preferCPU: true)
                    .CreateAccelerator(context);
                var stream = accelerator.DefaultStream;
                using var buffer = stream.Allocate1D<int>(4);
                var view = buffer.View;
                stream.Launch((Index1D)4, index => K.First(index, view));
                stream.Launch((Index1D)4, index => K.Second(index, view));
                stream.Synchronize();
            }
        }
        """;

    [Fact]
    public async Task Build_ReportsCycleEnteredAtDifferentMethodsOnce()
    {
        var compilation = RoslynCompiler.CreateCompilation(
            [MutualBuildSource],
            "MutualRecursionTest");
        var analysis = CompilationRewriter.Analyze(compilation);
        Assert.Empty(analysis.Diagnostics);

        var provider = await MainKernelProvider.CreateAsync(
            compilation,
            analysis,
            [CompilationHelper.CreateBackend(BackendType.CPU)],
            new KernelCompiler());

        Assert.Contains(
            "): warning ILGPU1210: Methods 'IsEven', 'IsOdd' call each other",
            Assert.Single(provider.Diagnostics).ToString(),
            StringComparison.Ordinal);
    }

    #endregion
}
