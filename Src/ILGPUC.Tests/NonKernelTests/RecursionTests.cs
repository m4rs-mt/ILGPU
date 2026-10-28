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
/// (ILGPU1210), and that backends without recursive calls reject them (ILGPU1211).
/// Calls of a method itself in tail position become loops, and calls with constant
/// arguments are evaluated at compile time.
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

        public static void SelfCallingKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            if (data[index] <= 0)
                return;
            data[index] -= 1;
            SelfCallingKernel(index, data); // SelfCallingKernel
        }

        private static void CallKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data) =>
            KernelCallingItselfThroughHelper(index, data); // CallKernel

        public static void KernelCallingItselfThroughHelper(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            if (data[index] <= 0)
                return;
            data[index] -= 1;
            CallKernel(index, data);
        }

        public struct Wrap<T>
        {
            public T Value;
        }

        private static int Nest<T>(int n, T value)
            where T : struct =>
            n <= 0 ? 0 : 1 + Nest(n - 1, new Wrap<T> { Value = value }); // Nest

        public static void PolymorphicRecursionKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Nest(data[index], 0);
        }

        private static int Unwrap<T>(T value)
            where T : struct =>
            value is Wrap<Wrap<Wrap<int>>> wrapped ? wrapped.Value.Value.Value : 0;

        public static void NestedTypeArgumentsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var value = default(Wrap<Wrap<Wrap<int>>>);
            value.Value.Value.Value = data[index];
            data[index] = Unwrap(value);
        }

        private static bool Never() => false;

        public static void UnreachableRecursionKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            if (Never())
                data[index] = Factorial(data[index]);
        }

        private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

        public static void GcdKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Gcd(data[index], 12);
        }

        private static int SumTo(int n, int sum)
        {
            if (n <= 0)
                return sum;
            int next = sum + n;
            return SumTo(n - 1, next);
        }

        public static void SumToKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = SumTo(data[index], 0);
        }

        private static void Fill(ArrayView1D<int, Stride1D.Dense> data, int i, int n)
        {
            if (i >= n)
                return;
            data[i] = i;
            Fill(data, i + 1, n);
        }

        public static void FillKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Fill(data, index * 2, index * 2 + 2);
        }

        private static int Steps<T>(int n, int steps) where T : struct =>
            n <= 1 ? steps : Steps<T>(n % 2 == 0 ? n / 2 : 3 * n + 1, steps + 1);

        public static void StepsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Steps<float>(data[index], 0);
        }

        private static void Increment(ref int value, int n)
        {
            if (n <= 0)
                return;
            value++;
            Increment(ref value, n - 1); // Increment
        }

        public static void IncrementKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int value = 0;
            Increment(ref value, data[index]);
            data[index] = value;
        }

        private static int CountOdd(int n)
        {
            if (n <= 0)
                return 0;
            if (n % 2 == 0)
                return CountOdd(n - 1);
            return 1 + CountOdd(n - 1); // CountOdd
        }

        public static void CountOddKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = CountOdd(data[index]);
        }

        public static void ConstantFactorialKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Factorial(5) + index;
        }

        public static void ConstantMutualRecursionKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = IsEven(10) ? index : 0;
        }

        private static int SumBelow(int n) =>
            n <= 0 ? 0 : n + SumBelow(n - 1); // SumBelow

        public static void DeepConstantRecursionKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = SumBelow(1000);
        }

        private struct Pair
        {
            public int A;
            public int B;

            public readonly int Sum() => A + B;
        }

        private static int SumPairs(int n, int sum)
        {
            var pair = new Pair { A = n, B = 1 };
            if (n <= 0)
                return sum;
            return SumPairs(n - 1, sum + pair.Sum());
        }

        public static void SumPairsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = SumPairs(data[index], 0);
        }

        private static int Count(nint p, int n, int count) =>
            n <= 0 ? count : Count(p, n - 1, count + 1);

        public static void CountKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Count(0, data[index], 0);
        }

        private static void Add(ref int value, int amount) => value += amount;

        private static int Accumulate(int n, int sum)
        {
            int x = sum;
            if (n <= 0)
                return sum;
            Add(ref x, n);
            return Accumulate(n - 1, x);
        }

        public static void AccumulateKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Accumulate(data[index], 0);
        }

        private static unsafe int Locals(int n, nint p, int sum)
        {
            int x = n;
            if (n == 0)
                return sum;
            return Locals(n - 1, (nint)(&x), sum + *(int*)p); // Locals
        }

        public static unsafe void LocalsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int start = 0;
            data[index] = Locals(data[index], (nint)(&start), 0);
        }

        private static unsafe int Arguments(int n, nint p, int sum) =>
            n == 0 ? sum : Arguments(n - 1, (nint)(&n), sum + *(int*)p); // Arguments

        public static unsafe void ArgumentsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int start = 0;
            data[index] = Arguments(data[index], (nint)(&start), 0);
        }

        private static unsafe void GetAddress(ref int value, out nint address)
        {
            fixed (int* pointer = &value)
                address = (nint)pointer;
        }

        private static unsafe int Outputs(int n, nint p, int sum)
        {
            int x = n;
            if (n == 0)
                return sum;
            GetAddress(ref x, out nint next);
            return Outputs(n - 1, next, sum + *(int*)p); // Outputs
        }

        public static unsafe void OutputsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int start = 0;
            data[index] = Outputs(data[index], (nint)(&start), 0);
        }

        private static unsafe int* AddressOf(ref int value)
        {
            fixed (int* address = &value)
                return address;
        }

        private static unsafe int Slots(
            ArrayView1D<nint, Stride1D.Dense> slots,
            int n,
            int sum)
        {
            int x = n;
            if (n == 0)
                return sum;
            int value = *(int*)slots[0];
            slots[0] = (nint)AddressOf(ref x);
            return Slots(slots, n - 1, sum + value); // Slots
        }

        public static unsafe void SlotsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<nint, Stride1D.Dense> slots)
        {
            int start = 0;
            slots[0] = (nint)(&start);
            data[index] = Slots(slots, data[index], 0);
        }

        private static unsafe void Remember(
            ref int value,
            ArrayView1D<nint, Stride1D.Dense> slots)
        {
            fixed (int* address = &value)
                slots[0] = (nint)address;
        }

        private static unsafe int Callees(
            ArrayView1D<nint, Stride1D.Dense> slots,
            int n,
            int sum)
        {
            int x = n;
            if (n == 0)
                return sum;
            int value = *(int*)slots[0];
            Remember(ref x, slots);
            return Callees(slots, n - 1, sum + value); // Callees
        }

        public static unsafe void CalleesKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<nint, Stride1D.Dense> slots)
        {
            int start = 0;
            slots[0] = (nint)(&start);
            data[index] = Callees(slots, data[index], 0);
        }

        private static void Forward(
            ref int value,
            ArrayView1D<nint, Stride1D.Dense> slots) =>
            Remember(ref value, slots);

        private static unsafe int NestedCallees(
            ArrayView1D<nint, Stride1D.Dense> slots,
            int n,
            int sum)
        {
            int x = n;
            if (n == 0)
                return sum;
            int value = *(int*)slots[0];
            Forward(ref x, slots);
            return NestedCallees(slots, n - 1, sum + value); // NestedCallees
        }

        public static unsafe void NestedCalleesKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<nint, Stride1D.Dense> slots)
        {
            int start = 0;
            slots[0] = (nint)(&start);
            data[index] = NestedCallees(slots, data[index], 0);
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

    [Theory]
    [InlineData(BackendType.Metal)]
    [InlineData(BackendType.OpenCL)]
    public void BackendsWithoutRecursiveCalls_RejectRecursion(BackendType backend)
    {
        using var helper = new CompilationHelper(backend);
        var exception = Assert.Throws<KernelCompilationException>(() =>
            helper.GenerateBackendCode(
                GetKernel(nameof(Kernels.FactorialKernel)),
                backend));

        Assert.Equal("ILGPU1211", exception.Id);
        Assert.Equal(
            $"Recursive calls of 'Factorial' are not supported by the {backend} " +
            "backend.",
            exception.Message);
        AssertLocation(exception.Location, "Factorial");
    }

    [Theory]
    [InlineData(BackendType.CPU)]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.Metal)]
    [InlineData(BackendType.OpenCL)]
    public void KernelCallingItself_IsRejected(BackendType backend)
    {
        using var helper = new CompilationHelper(backend);
        var exception = Assert.Throws<KernelCompilationException>(() =>
            helper.GenerateBackendCode(
                GetKernel(nameof(Kernels.SelfCallingKernel)),
                backend));

        Assert.Equal("ILGPU1212", exception.Id);
        Assert.Equal(
            "Kernel 'SelfCallingKernel' calls itself, directly or through other " +
            "methods, which is not supported. Move the code that calls itself into " +
            "a helper method that the kernel calls.",
            exception.Message);
        AssertLocation(exception.Location, "SelfCallingKernel");
    }

    [Theory]
    [InlineData(BackendType.CPU)]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.Metal)]
    [InlineData(BackendType.OpenCL)]
    public void KernelCallingItselfThroughHelper_IsRejected(BackendType backend)
    {
        using var helper = new CompilationHelper(backend);
        var exception = Assert.Throws<KernelCompilationException>(() =>
            helper.GenerateBackendCode(
                GetKernel(nameof(Kernels.KernelCallingItselfThroughHelper)),
                backend));

        Assert.Equal("ILGPU1212", exception.Id);
        Assert.StartsWith(
            "Kernel 'KernelCallingItselfThroughHelper' calls itself",
            exception.Message,
            StringComparison.Ordinal);
        AssertLocation(exception.Location, "CallKernel");
    }

    [Theory]
    [InlineData(BackendType.Metal)]
    [InlineData(BackendType.OpenCL)]
    public async Task BackendsWithoutRecursiveCalls_AcceptRecursionRemovedByOptimization(
        BackendType backend)
    {
        // The frontend still compiles the recursive call, which optimization removes
        Assert.Single(await GetWarningsAsync(nameof(Kernels.UnreachableRecursionKernel)));

        using var helper = new CompilationHelper(backend);
        helper.GenerateBackendCode(
            GetKernel(nameof(Kernels.UnreachableRecursionKernel)),
            backend);
    }

    #endregion

    #region Generic Nesting

    [Fact]
    public void PolymorphicRecursion_IsRejected()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var exception = Assert.Throws<KernelCompilationException>(() =>
            helper.GenerateBackendCode(
                GetKernel(nameof(Kernels.PolymorphicRecursionKernel)),
                BackendType.CPU));

        Assert.Equal("ILGPU1213", exception.Id);
        Assert.Equal(
            "Method 'Nest' calls 'Nest' with type arguments nested more than 32 " +
            "levels deep, as a generic method does that calls itself with a type " +
            "argument wrapping its own. Such calls create new instantiations " +
            "without end, which is not supported in kernels.",
            exception.Message);
        AssertLocation(exception.Location, "Nest");
    }

    [Fact]
    public async Task NestedTypeArguments_AreCompiled() =>
        Assert.Empty(await GetWarningsAsync(nameof(Kernels.NestedTypeArgumentsKernel)));

    #endregion

    #region Tail Calls

    [Theory]
    [InlineData(nameof(Kernels.GcdKernel), "Gcd")]
    [InlineData(nameof(Kernels.SumToKernel), "SumTo")]
    [InlineData(nameof(Kernels.FillKernel), "Fill")]
    [InlineData(nameof(Kernels.StepsKernel), "Steps")]
    [InlineData(nameof(Kernels.SumPairsKernel), "SumPairs")]
    [InlineData(nameof(Kernels.CountKernel), "Count")]
    [InlineData(nameof(Kernels.AccumulateKernel), "Accumulate")]
    public async Task TailRecursion_BecomesLoopThatIsInlined(
        string kernelName,
        string helperName)
    {
        Assert.Empty(await GetWarningsAsync(kernelName));

        var module = Optimize(kernelName);
        Assert.Empty(CallCycle.FindAll(module.Methods.ToArray()));
        Assert.DoesNotContain(
            module.Methods.ToArray(),
            method => method.Source?.Name == helperName);
    }

    [Theory]
    [InlineData(BackendType.Metal)]
    [InlineData(BackendType.OpenCL)]
    public void BackendsWithoutRecursiveCalls_CompileTailRecursion(BackendType backend)
    {
        using var helper = new CompilationHelper(backend);
        helper.GenerateBackendCode(GetKernel(nameof(Kernels.GcdKernel)), backend);
        helper.GenerateBackendCode(GetKernel(nameof(Kernels.SumToKernel)), backend);
        helper.GenerateBackendCode(GetKernel(nameof(Kernels.FillKernel)), backend);
    }

    [Fact]
    public async Task TailRecursionWithReferenceParameter_StaysRecursive()
    {
        // The reference could point to a variable of the calling method
        var warning = Assert.Single(
            await GetWarningsAsync(nameof(Kernels.IncrementKernel)));

        Assert.Equal("ILGPU1210", warning.Id);
        AssertLocation(warning.Location, "Increment");
    }

    [Theory]
    [InlineData(nameof(Kernels.LocalsKernel), "Locals")]
    [InlineData(nameof(Kernels.ArgumentsKernel), "Arguments")]
    [InlineData(nameof(Kernels.OutputsKernel), "Outputs")]
    [InlineData(nameof(Kernels.SlotsKernel), "Slots")]
    [InlineData(nameof(Kernels.CalleesKernel), "Callees")]
    [InlineData(nameof(Kernels.NestedCalleesKernel), "NestedCallees")]
    public async Task TailRecursionPassingAddresses_StaysRecursive(
        string kernelName,
        string helperName)
    {
        // The next call reads the variable of the calling one, which a loop would
        // have assigned again
        var warning = Assert.Single(await GetWarningsAsync(kernelName));

        Assert.Equal("ILGPU1210", warning.Id);
        AssertLocation(warning.Location, helperName);
    }

    [Fact]
    public async Task RecursionWithTailAndOtherCalls_ReportsWarningAtTheOtherCall()
    {
        var warning = Assert.Single(
            await GetWarningsAsync(nameof(Kernels.CountOddKernel)));

        Assert.Equal("ILGPU1210", warning.Id);
        AssertLocation(warning.Location, "CountOdd");
    }

    #endregion

    #region Constant Arguments

    [Theory]
    [InlineData(nameof(Kernels.ConstantFactorialKernel), "Factorial")]
    [InlineData(nameof(Kernels.ConstantMutualRecursionKernel), "IsEven")]
    public async Task RecursionWithConstantArguments_IsEvaluated(
        string kernelName,
        string helperName)
    {
        Assert.Empty(await GetWarningsAsync(kernelName));

        using var helper = new CompilationHelper(BackendType.CPU);
        var module = helper.CompileToModule(
            GetKernel(kernelName),
            new CompilationProperties());
        Assert.DoesNotContain(
            module.Methods.ToArray(),
            method => method.Source?.Name == helperName);
    }

    [Theory]
    [InlineData(BackendType.Metal)]
    [InlineData(BackendType.OpenCL)]
    public void BackendsWithoutRecursiveCalls_CompileRecursionWithConstantArguments(
        BackendType backend)
    {
        using var helper = new CompilationHelper(backend);
        helper.GenerateBackendCode(
            GetKernel(nameof(Kernels.ConstantFactorialKernel)),
            backend);
        helper.GenerateBackendCode(
            GetKernel(nameof(Kernels.ConstantMutualRecursionKernel)),
            backend);
    }

    [Fact]
    public async Task RecursionWithConstantArguments_DeeperThanTheLimit_StaysRecursive()
    {
        var warning = Assert.Single(
            await GetWarningsAsync(nameof(Kernels.DeepConstantRecursionKernel)));

        Assert.Equal("ILGPU1210", warning.Id);
        AssertLocation(warning.Location, "SumBelow");
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

    [Fact]
    public async Task Build_ForMetal_FailsWithRecursionError()
    {
        var compilation = RoslynCompiler.CreateCompilation(
            [BuildSource],
            "RecursionTest");
        var analysis = CompilationRewriter.Analyze(compilation);

        var exception = await Assert.ThrowsAsync<KernelDiagnosticException>(() =>
            MainKernelProvider.CreateAsync(
                compilation,
                analysis,
                [CompilationHelper.CreateBackend(BackendType.Metal)],
                new KernelCompiler()));

        var diagnostic = exception.Diagnostic.ToString();
        Assert.StartsWith(
            $"source0.cs({RecursionLine},",
            diagnostic,
            StringComparison.Ordinal);
        Assert.Contains(
            "): error ILGPU1211: Recursive calls of 'Factorial' are not supported " +
            "by the Metal backend.",
            diagnostic,
            StringComparison.Ordinal);
    }

    #endregion
}
