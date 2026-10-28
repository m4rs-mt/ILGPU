// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemoryCallSiteIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Random;
using ILGPU.Runtime;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Tests.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using IRModule = ILGPUC.IR.ModuleValues.Module;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Tests that a <c>Group.GetSharedMemory</c> or
/// <c>Group.GetLocalMemoryPerThread</c> call site yields one buffer per kernel:
/// a helper inlined twice and a helper that is not inlined reference the
/// global of the call site in the kernel at every optimization level. A
/// library helper that reuses its buffer, such as <c>Group.Permute</c>, ends
/// with a barrier after its last read.
/// </summary>
public sealed class SharedMemoryCallSiteIRTests
{
    /// <summary>
    /// Kernels compiled directly via <see cref="CompilationHelper"/>. This
    /// class is intentionally outside the <c>ILGPUC.Tests.Kernels</c>
    /// namespace so <see cref="KernelRegistry"/> does not enroll it in the
    /// backend test matrix.
    /// </summary>
    private static class RegressionKernels
    {
        private const int Length = 64;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ArrayView<int> Shared() => Group.GetSharedMemory<int>(Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ArrayView<int> Local() => Group.GetLocalMemoryPerThread<int>(4);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadNeighbour() =>
            Shared()[(Group.Index + 1) % Group.Dimension];

        public static void SharedTwiceKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            var first = Shared();
            var second = Shared();
            first[Group.Index] = data[index];
            Group.Barrier();
            data[index] = second[(Group.Index + 1) % Group.Dimension];
        }

        public static void LocalTwiceKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            var first = Local();
            var second = Local();
            first[data[index] & 3] = index;
            data[index] = second[index & 3];
        }

        public static void SharedAcrossCallKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            Shared()[Group.Index] = data[index];
            Group.Barrier();
            data[index] = ReadNeighbour();
        }

        public static void PermuteTwiceKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            var random = new ZeroRandom();
            int first = Group.Permute(data[index], ref random);
            data[index] = Group.Permute(first + 1, ref random);
        }
    }

    /// <summary>
    /// A random provider that always returns zero.
    /// </summary>
    private struct ZeroRandom : IRandomProvider
    {
        public readonly int Next() => 0;

        public readonly long NextLong() => 0;

        public readonly float NextFloat() => 0;

        public readonly double NextDouble() => 0;

        public readonly void ShiftPeriod(int shift) { }
    }

    /// <summary>
    /// Returns the globals that the values of the given methods use, directly
    /// or through their operands.
    /// </summary>
    private static HashSet<Global> GetGlobals(IEnumerable<Method> methods)
    {
        var globals = new HashSet<Global>();
        var visited = new HashSet<Value>();
        var worklist = new Stack<Value>();
        foreach (var method in methods)
        {
            foreach (var block in method.Blocks)
            {
                foreach (var (value, _) in block.BasicBlockValues)
                    worklist.Push(value);
            }
        }
        while (worklist.TryPop(out var value))
        {
            if (!visited.Add(value))
                continue;
            if (value is Global global)
                globals.Add(global);
            foreach (var operand in value.Values)
                worklist.Push(operand);
        }
        return globals;
    }

    /// <summary>
    /// Compiles the given kernel and optimizes it at the given level, or returns
    /// the module of the frontend for <see langword="null"/>.
    /// </summary>
    private static IRModule Compile(
        CompilationHelper helper,
        string kernelName,
        OptimizationLevel? level)
    {
        var kernel = typeof(RegressionKernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;
        var properties = new CompilationProperties(
            OptimizationLevel: level ?? OptimizationLevel.O0);
        var module = helper.CompileToModule(kernel, properties);
        return level is { } optimizationLevel
            ? optimizationLevel.CreateTransformer().Apply(
                properties,
                helper.TypeManager,
                module)
            : module;
    }

    public static TheoryData<string, bool, OptimizationLevel?> CallSiteKernels
    {
        get
        {
            var data = new TheoryData<string, bool, OptimizationLevel?>();
            OptimizationLevel?[] levels =
            [
                OptimizationLevel.O0,
                OptimizationLevel.O1,
                OptimizationLevel.O2,
            ];
            foreach (var level in levels)
            {
                data.Add(
                    nameof(RegressionKernels.SharedTwiceKernel),
                    false,
                    level);
                data.Add(
                    nameof(RegressionKernels.LocalTwiceKernel),
                    true,
                    level);
                data.Add(
                    nameof(RegressionKernels.SharedAcrossCallKernel),
                    false,
                    level);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CallSiteKernels))]
    public void CallSite_YieldsOneBufferPerKernel(
        string kernelName,
        bool local,
        OptimizationLevel? level)
    {
        using var helper = new CompilationHelper();
        var module = Compile(helper, kernelName, level);
        var addressSpace = local
            ? MemoryAddressSpace.Local
            : MemoryAddressSpace.Shared;

        // The kernel, its inlined helpers and the helper that is not inlined
        // use the one global of the call site
        var buffers = module.Globals.ToArray()
            .Where(global => global.AddressSpace == addressSpace)
            .ToArray();
        var buffer = Assert.Single(buffers);
        var methods = module.Methods.ToArray()
            .Where(method => method.HasImplementation)
            .ToArray();
        var users = methods
            .Where(method => GetGlobals([method]).Contains(buffer))
            .Select(method => method.Source?.Name)
            .ToArray();
        Assert.Contains(kernelName, users);
        if (kernelName == nameof(RegressionKernels.SharedAcrossCallKernel))
            Assert.Contains("ReadNeighbour", users);
        Assert.Single(
            GetGlobals(methods),
            global => global.AddressSpace == addressSpace);
    }

    [Fact]
    public void Permute_EndsWithBarrierAfterItsLastRead()
    {
        using var helper = new CompilationHelper();
        var module = Compile(
            helper,
            nameof(RegressionKernels.PermuteTwiceKernel),
            level: null);

        // Both calls of the helper use the buffer of its one call site, so the
        // second call must not store to it before every thread has read the first
        var permute = Assert.Single(
            module.Methods.ToArray(),
            method => method.HasImplementation &&
                method.Source is MethodInfo { Name: "Permute" } source &&
                source.DeclaringType == typeof(Group) &&
                source.GetParameters() is [_, { ParameterType: var type }] &&
                type == typeof(int));
        var values = new List<Value>();
        foreach (var block in permute.Blocks)
        {
            foreach (var (value, _) in block.BasicBlockValues)
                values.Add(value);
        }
        int lastLoad = values.FindLastIndex(value => value is Load);
        Assert.True(lastLoad >= 0, "expected a load of the shared buffer");
        Assert.Contains(
            values.Skip(lastLoad + 1),
            value => value is Barrier { Kind: BarrierKind.GroupLevel });
    }
}
