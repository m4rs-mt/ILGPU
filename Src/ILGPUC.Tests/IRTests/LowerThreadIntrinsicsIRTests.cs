// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerThreadIntrinsicsIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPU;
using ILGPU.Runtime;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Tests.Framework;
using System.Reflection;
using Xunit;
using IRModule = ILGPUC.IR.ModuleValues.Module;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Regression tests for <see cref="LowerThreadIntrinsics"/>: built-in
/// (primitive) broadcasts and shuffles must be left unchanged by the pass.
/// Under the converter contract, returning <c>null</c> from a converter
/// deletes the value — the pass must return the same reference instead.
/// A collective on a pointer is rejected with ILGPU1200.
/// </summary>
public sealed class LowerThreadIntrinsicsIRTests
{
    /// <summary>
    /// Kernels compiled directly via <see cref="CompilationHelper"/>. This
    /// class is intentionally outside the <c>ILGPUC.Tests.Kernels</c>
    /// namespace so <see cref="KernelRegistry"/> does not enroll it in the
    /// backend test matrix.
    /// </summary>
    private static class RegressionKernels
    {
        /// <summary>
        /// Contains a built-in (Int32) warp shuffle.
        /// </summary>
        public static void BuiltInShuffleKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Warp.Shuffle(data[index], 1);
        }

        /// <summary>
        /// Contains a built-in (Int32) warp broadcast.
        /// </summary>
        public static void BuiltInBroadcastKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Warp.Broadcast(data[index]);
        }

        public static void WarpReduceKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Warp.Reduce(data[index], (x, y) => x + y);
        }

        public static void WarpExclusiveScanKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Warp.ExclusiveScan(data[index], 0, (x, y) => x + y);
        }

        public static void GroupReduceKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Group.Reduce(data[index], 0, (x, y) => x + y);
        }

        public static void GroupInclusiveScanKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Group.InclusiveScan(data[index], 0, (x, y) => x + y);
        }
    }

    /// <summary>
    /// Turns every <see cref="WarpReduce"/>, <see cref="WarpScan"/>,
    /// <see cref="GroupReduce"/> and <see cref="GroupScan"/> into an <c>Add</c>
    /// collective of the same kind over its value as a pointer, with its identity
    /// as a pointer, and returns the result as an integer. The frontend never
    /// builds a pointer collective with a recognized operation, as C# has no
    /// such operation on two pointers, so this pass stands in for one.
    /// </summary>
    private sealed class PointerCollectives(TransformationArgs args) :
        Transformation(args)
    {
        protected override void OnMap(ModuleTransform transform)
        {
            base.OnMap(transform);

            // Each converter creates the pointer collective as an operand of
            // the cast it returns, which only a deferred mapping places in the
            // block
            DeferBlockValueMapping = true;
            MapBasicBlockValue<WarpReduce>(static (transform, value) =>
                GetInteger(transform, value, transform.CreateWarpReduce(
                    value.Location,
                    GetPointer(transform, value.Variable),
                    BinaryArithmeticKind.Add,
                    ArithmeticFlags.None,
                    value.Kind)));
            MapBasicBlockValue<WarpScan>(static (transform, value) =>
                GetInteger(transform, value, transform.CreateWarpScan(
                    value.Location,
                    GetPointer(transform, value.Variable),
                    BinaryArithmeticKind.Add,
                    ArithmeticFlags.None,
                    value.Kind,
                    GetPointer(transform, value.Identity))));
            MapBasicBlockValue<GroupReduce>(static (transform, value) =>
                GetInteger(transform, value, transform.CreateGroupReduce(
                    value.Location,
                    GetPointer(transform, value.Variable),
                    BinaryArithmeticKind.Add,
                    ArithmeticFlags.None,
                    value.Kind,
                    GetPointer(transform, value.Identity))));
            MapBasicBlockValue<GroupScan>(static (transform, value) =>
                GetInteger(transform, value, transform.CreateGroupScan(
                    value.Location,
                    GetPointer(transform, value.Variable),
                    BinaryArithmeticKind.Add,
                    ArithmeticFlags.None,
                    value.Kind,
                    GetPointer(transform, value.Identity))));
        }

        private static Value? GetPointer(
            BasicBlockTransform transform,
            Value? value) =>
            value is null
            ? null
            : transform.CreateIntAsPointerCast(
                value.Location,
                transform.Rewrite(value));

        private static Value? GetInteger(
            BasicBlockTransform transform,
            ThreadValue value,
            Value? collective) =>
            transform.CreatePointerAsIntCast(
                value.Location,
                collective,
                value.BasicValueType);
    }

    /// <summary>
    /// Counts values of type <typeparamref name="T"/> across all implemented
    /// methods of the given module.
    /// </summary>
    private static int CountValues<T>(IRModule module)
        where T : class
    {
        int count = 0;
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation)
                continue;
            foreach (var block in method.Blocks)
            {
                foreach (var (value, _) in block.BasicBlockValues)
                {
                    if (value is T)
                        ++count;
                }
            }
        }
        return count;
    }

    /// <summary>
    /// Compiles the given kernel, applies <see cref="LowerThreadIntrinsics"/>
    /// in isolation, and returns the number of <typeparamref name="T"/>
    /// values before and after the pass.
    /// </summary>
    private static (int Before, int After) ApplyPass<T>(string kernelName)
        where T : class
    {
        using var helper = new CompilationHelper();
        var kernel = typeof(RegressionKernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;

        var properties = new CompilationProperties();
        var module = helper.CompileToModule(kernel, properties);
        int before = CountValues<T>(module);

        var transformer = Transformer.Create(
            static args => new LowerThreadIntrinsics(args));
        var transformed = transformer.Apply(
            properties, helper.TypeManager, module);
        int after = CountValues<T>(transformed);

        return (before, after);
    }

    [Theory]
    [InlineData(nameof(RegressionKernels.WarpReduceKernel), "Warp.Reduce")]
    [InlineData(
        nameof(RegressionKernels.WarpExclusiveScanKernel),
        "Warp.ExclusiveScan")]
    [InlineData(nameof(RegressionKernels.GroupReduceKernel), "Group.Reduce")]
    [InlineData(
        nameof(RegressionKernels.GroupInclusiveScanKernel),
        "Group.InclusiveScan")]
    public void PointerCollective_IsRejected(string kernelName, string collective)
    {
        using var helper = new CompilationHelper();
        var kernel = typeof(RegressionKernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;

        var properties = new CompilationProperties();
        var module = helper.CompileToModule(kernel, properties);

        // A pointer has the basic value type of an integer, but no operation
        // combines two addresses
        var transformer = Transformer.Create(
            static args => new PointerCollectives(args),
            static args => new LowerThreadIntrinsics(args));
        var exception = Assert.Throws<KernelCompilationException>(
            () => transformer.Apply(properties, helper.TypeManager, module));
        Assert.Equal("ILGPU1200", exception.Id);
        Assert.Equal(
            $"{collective} does not support values of type 'ptr<void, Generic>'. " +
            "Only primitive types and structures of primitive types can be " +
            "broadcast, shuffled, reduced or scanned.",
            exception.Message);
    }

    [Fact]
    public void BuiltInShuffle_SurvivesLowerThreadIntrinsics()
    {
        var (before, after) = ApplyPass<Shuffle>(
            nameof(RegressionKernels.BuiltInShuffleKernel));

        // The kernel must actually contain a built-in shuffle...
        Assert.True(before > 0, "expected a Shuffle value in the raw module");
        // ...and the pass must leave it unchanged instead of deleting it.
        Assert.Equal(before, after);
    }

    [Fact]
    public void BuiltInBroadcast_SurvivesLowerThreadIntrinsics()
    {
        var (before, after) = ApplyPass<Broadcast>(
            nameof(RegressionKernels.BuiltInBroadcastKernel));

        Assert.True(before > 0, "expected a Broadcast value in the raw module");
        Assert.Equal(before, after);
    }
}
