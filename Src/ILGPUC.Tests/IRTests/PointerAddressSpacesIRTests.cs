// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerAddressSpacesIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Tests.Framework;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Interprocedural <see cref="PointerAddressSpaces"/> analysis: the address
/// space of every call argument must flow into the matching callee parameter.
/// </summary>
public sealed class PointerAddressSpacesIRTests
{
    /// <summary>
    /// Kernels compiled directly via <see cref="CompilationHelper"/>. This
    /// class is intentionally outside the <c>ILGPUC.Tests.Kernels</c>
    /// namespace so <see cref="KernelRegistry"/> does not enroll it in the
    /// backend test matrix.
    /// </summary>
    private static class AddressSpaceKernels
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Store(int offset, ArrayView<int> target)
        {
            target[offset] = offset;
        }

        /// <summary>
        /// Passes a global view as the second argument of a helper, behind a
        /// scalar. Pairing parameter i with operand i of the call (whose
        /// operand 0 is the target method) shifts the view's address space
        /// off its parameter.
        /// </summary>
        public static void CallKernel(Index1D index, ArrayView<int> data)
        {
            Store(index, data);
        }
    }

    [Fact]
    public void CallArguments_FlowIntoMatchingParameters()
    {
        using var helper = new CompilationHelper();
        var kernel = typeof(AddressSpaceKernels).GetMethod(
            nameof(AddressSpaceKernels.CallKernel),
            BindingFlags.Public | BindingFlags.Static)!;
        var properties = new CompilationProperties();
        var frontendModule = helper.CompileToModule(kernel, properties);

        // Promote locals to SSA values (without inlining) so that the views
        // flow directly from their definitions into the helper call.
        var builder = Transformer.CreateBuilder();
        builder.Add(static args => new SimplifyControlFlow(args));
        builder.Add(static args => new SSAConstruction(args));
        builder.Add(static args => new SSACleanup(args));
        var module = builder.ToTransformer().Apply(
            properties, helper.TypeManager, frontendModule);

        Method? entry = null;
        Method? callee = null;
        foreach (var method in module.Methods)
        {
            if (method.Name.Contains(nameof(AddressSpaceKernels.Store)))
                callee = method;
            else if (method.Name.Contains(nameof(AddressSpaceKernels.CallKernel)))
                entry = method;
        }
        Assert.NotNull(entry);
        Assert.NotNull(callee);
        Assert.True(callee.HasImplementation, "the helper must stay a separate method");

        // Same flags as the InferAddressSpaces pass
        var results = PointerAddressSpaces.AnalyzeModule(
            entry,
            PointerAddressSpaces.AnalysisFlags.IgnoreGenericAddressSpace);

        var offset = results[callee.Parameters[0]];
        var target = results[callee.Parameters[1]];
        Assert.True(
            target.HasAddressSpace(MemoryAddressSpace.Global),
            $"view parameter: {target}");
        Assert.False(
            offset.HasAddressSpace(MemoryAddressSpace.Global),
            $"scalar parameter: {offset}");
    }
}
