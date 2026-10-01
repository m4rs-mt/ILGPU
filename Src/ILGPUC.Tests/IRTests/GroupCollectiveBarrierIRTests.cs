// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupCollectiveBarrierIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Tests.Framework;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Tests that <see cref="LowerGroupCollectives"/> ends a group reduction and a
/// group scan with a group barrier after its last read of its shared memory, so
/// that the next call from the same call site, such as in the next iteration of
/// a loop, cannot overwrite a slot that a slower warp has not read. The kernels
/// use method groups, which the frontend recognizes without inlining.
/// </summary>
public sealed class GroupCollectiveBarrierIRTests
{
    /// <summary>
    /// Kernels compiled directly via <see cref="CompilationHelper"/>. This
    /// class is intentionally outside the <c>ILGPUC.Tests.Kernels</c>
    /// namespace so <see cref="KernelRegistry"/> does not enroll it in the
    /// backend test matrix.
    /// </summary>
    private static class RegressionKernels
    {
        public static void GroupReduceKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Group.Reduce(data[index], int.MinValue, XMath.Max);
        }

        public static void GroupAllReduceKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Group.AllReduce(data[index], int.MinValue, XMath.Max);
        }

        public static void GroupInclusiveScanKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Group.InclusiveScan(data[index], int.MaxValue, XMath.Min);
        }

        public static void GroupExclusiveScanKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Group.ExclusiveScan(data[index], int.MinValue, XMath.Max);
        }
    }

    /// <summary>
    /// Returns true if the given value reaches a shared memory global through
    /// its operands.
    /// </summary>
    private static bool UsesSharedMemory(Value value)
    {
        var visited = new HashSet<Value>();
        var worklist = new Stack<Value>();
        worklist.Push(value);
        while (worklist.TryPop(out var current))
        {
            if (!visited.Add(current))
                continue;
            if (current is Global { AddressSpace: MemoryAddressSpace.Shared })
                return true;
            foreach (var operand in current.Values)
                worklist.Push(operand);
        }
        return false;
    }

    [Theory]
    [InlineData(nameof(RegressionKernels.GroupReduceKernel))]
    [InlineData(nameof(RegressionKernels.GroupAllReduceKernel))]
    [InlineData(nameof(RegressionKernels.GroupInclusiveScanKernel))]
    [InlineData(nameof(RegressionKernels.GroupExclusiveScanKernel))]
    public void GroupCollective_EndsWithBarrierAfterItsLastSharedRead(
        string kernelName)
    {
        using var helper = new CompilationHelper();
        var kernel = typeof(RegressionKernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;
        var properties = new CompilationProperties();
        var module = helper.CompileToModule(kernel, properties);

        // Groups of several warps of 32 threads
        var specification = new ArchitectureSpecification(
            CPUAcceleratorCapabilities.Default,
            WarpSize: 32,
            new AcceleratorArchitecture(0, 0),
            SupportsViews: false);
        var transformer = Transformer.Create(
            args => new LowerGroupCollectives(args, specification));
        var lowered = transformer.Apply(properties, helper.TypeManager, module);

        var values = new List<Value>();
        foreach (var method in lowered.Methods)
        {
            if (!method.HasImplementation || method.Source?.Name != kernelName)
                continue;
            foreach (var block in method.Blocks)
            {
                foreach (var (value, _) in block.BasicBlockValues)
                    values.Add(value);
            }
        }
        int lastRead = values.FindLastIndex(
            value => value is Load load && UsesSharedMemory(load.Source));
        Assert.True(lastRead >= 0, "expected a load of shared memory");
        Assert.Contains(
            values.GetRange(lastRead + 1, values.Count - lastRead - 1),
            value => value is Barrier { Kind: BarrierKind.GroupLevel });
    }
}
