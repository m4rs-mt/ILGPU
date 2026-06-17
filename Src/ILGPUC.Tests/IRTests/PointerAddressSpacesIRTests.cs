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
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Tests.Framework;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using IRModule = ILGPUC.IR.ModuleValues.Module;
using SpaceKernels = ILGPUC.Tests.Kernels.AddressSpaceKernels;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Interprocedural <see cref="PointerAddressSpaces"/> analysis: the address
/// space of every call argument must flow into the matching callee parameter,
/// and <see cref="InferAddressSpaces"/> must specialize references whose
/// sources all agree.
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

    /// <summary>
    /// Compiles the given address space kernel through the global optimization
    /// pipeline (including <see cref="InferAddressSpaces"/>).
    /// </summary>
    private static IRModule CompileOptimized(
        CompilationHelper helper,
        string kernelName,
        OptimizationLevel level = OptimizationLevel.O1)
    {
        var kernel = typeof(SpaceKernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;
        var properties = new CompilationProperties { OptimizationLevel = level };
        var module = helper.CompileToModule(kernel, properties);
        return level.CreateTransformer().Apply(properties, helper.TypeManager, module);
    }

    /// <summary>
    /// Returns the single pointer-typed phi of the given method.
    /// </summary>
    private static PhiValue GetPointerPhi(Method method)
    {
        PhiValue? result = null;
        foreach (var block in method.Blocks)
        {
            foreach (PhiValue phi in block.PhiValues)
            {
                if (phi.Type is PointerType)
                {
                    Assert.Null(result);
                    result = phi;
                }
            }
        }
        Assert.NotNull(result);
        return result;
    }

    /// <summary>
    /// Returns the method of the module whose name contains the given name.
    /// </summary>
    private static Method GetMethod(IRModule module, string name)
    {
        foreach (var method in module.Methods)
        {
            if (method.Name.Contains(name))
                return method;
        }
        Assert.Fail($"method {name} not found");
        return null!;
    }

    /// <summary>
    /// A loop-carried reference into shared memory is a phi of pure element
    /// addresses. All of them point into shared memory, so the phi is
    /// specialized and keeps both of its arguments.
    /// </summary>
    [Theory]
    [InlineData(OptimizationLevel.O0)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    public void LoopPointerPhi_IsSpecializedToShared(OptimizationLevel level)
    {
        using var helper = new CompilationHelper();
        var module = CompileOptimized(
            helper,
            nameof(SpaceKernels.SharedPointerLoopKernel),
            level);

        var phi = GetPointerPhi(module.EntryPoint);
        Assert.Equal(2, phi.NumArguments);
        Assert.Equal(
            MemoryAddressSpace.Shared,
            phi.GetTypeAs<PointerType>().AddressSpace);
        foreach (var argument in phi.Arguments)
            Assert.Equal(phi.Type, argument.Type);
    }

    /// <summary>
    /// A reference selecting between shared and global memory mixes address
    /// spaces: the phi stays generic and keeps both of its arguments, which
    /// are cast to the generic address space.
    /// </summary>
    [Theory]
    [InlineData(OptimizationLevel.O0)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    public void MixedSpacePhi_StaysGeneric(OptimizationLevel level)
    {
        using var helper = new CompilationHelper();
        var module = CompileOptimized(
            helper,
            nameof(SpaceKernels.MixedSpaceRefKernel),
            level);

        var phi = GetPointerPhi(module.EntryPoint);
        Assert.Equal(2, phi.NumArguments);
        Assert.Equal(
            MemoryAddressSpace.Generic,
            phi.GetTypeAs<PointerType>().AddressSpace);
        foreach (var argument in phi.Arguments)
            Assert.Equal(phi.Type, argument.Type);
    }

    /// <summary>
    /// A reference passed to a helper method is an address computation (a
    /// pure value). The helper's parameter inherits the address space of the
    /// referenced memory.
    /// </summary>
    private static void VerifyHelperParameter(
        string kernelName,
        MemoryAddressSpace expected)
    {
        using var helper = new CompilationHelper();
        var module = CompileOptimized(helper, kernelName);

        var increment = GetMethod(module, "Increment");
        Assert.True(increment.HasImplementation, "the helper must not be inlined");
        Assert.Equal(1, increment.NumParameters);
        Assert.Equal(
            expected,
            increment.Parameters[0].GetTypeAs<PointerType>().AddressSpace);
    }

    [Fact]
    public void SharedHelperParameter_IsSpecializedToShared() =>
        VerifyHelperParameter(
            nameof(SpaceKernels.SharedRefHelperKernel),
            MemoryAddressSpace.Shared);

    [Fact]
    public void LocalHelperParameter_IsSpecializedToLocal() =>
        VerifyHelperParameter(
            nameof(SpaceKernels.LocalRefHelperKernel),
            MemoryAddressSpace.Local);

    /// <summary>
    /// At O0, the view of <see cref="Kernels.BasicPhiKernels.DeepPhiKernel"/>
    /// is accessed through a local copy whose address flows through a phi.
    /// Every phi must keep one argument per predecessor.
    /// </summary>
    [Fact]
    public void DeepPhi_O0_KeepsAllPhiArguments()
    {
        using var helper = new CompilationHelper();
        var kernel = typeof(Kernels.BasicPhiKernels).GetMethod(
            nameof(Kernels.BasicPhiKernels.DeepPhiKernel),
            BindingFlags.Public | BindingFlags.Static)!;
        var properties = new CompilationProperties
        {
            OptimizationLevel = OptimizationLevel.O0
        };
        var module = OptimizationLevel.O0.CreateTransformer().Apply(
            properties,
            helper.TypeManager,
            helper.CompileToModule(kernel, properties));

        int numPointerPhis = 0;
        foreach (var block in module.EntryPoint.Blocks)
        {
            foreach (PhiValue phi in block.PhiValues)
            {
                Assert.Equal(block.Predecessors.Length, phi.NumArguments);
                if (phi.Type is PointerType)
                    ++numPointerPhis;
            }
        }
        Assert.Equal(1, numPointerPhis);
    }

    /// <summary>
    /// A helper returning a reference derived from its parameter: the parameter
    /// is specialized, while the returned reference is cast back to the generic
    /// return type.
    /// </summary>
    [Fact]
    public void ReturnedReference_KeepsGenericReturnType()
    {
        using var helper = new CompilationHelper();
        var module = CompileOptimized(
            helper,
            nameof(SpaceKernels.SharedRefReturnKernel));

        var next = GetMethod(module, "Next");
        Assert.True(next.HasImplementation, "the helper must not be inlined");
        Assert.Equal(
            MemoryAddressSpace.Shared,
            next.Parameters[0].GetTypeAs<PointerType>().AddressSpace);
        Assert.Equal(
            MemoryAddressSpace.Generic,
            next.GetTypeAs<PointerType>().AddressSpace);
        foreach (var block in next.Blocks)
        {
            if (ReturnView.TryCreate(block, out var returnView))
                Assert.Equal(next.Type, returnView.ReturnValue.Type);
        }
    }
}
