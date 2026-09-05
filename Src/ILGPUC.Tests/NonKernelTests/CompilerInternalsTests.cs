// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilerInternalsTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Frontend;
using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.Tests.Framework;
using ILGPUC.Util;
using System;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Unit-level regression tests for small compiler-internal helpers that have
/// no kernel-level seam.
/// </summary>
public sealed class CompilerInternalsTests
{
    #region UseCollection.TryGetSingleUse

    [Fact]
    public void TryGetSingleUse_Empty_ReturnsFalse()
    {
        var collection = new UseCollection(null!, []);
        Assert.False(collection.TryGetSingleUse(out _));
    }

    [Fact]
    public void TryGetSingleUse_SingleUse_ReturnsTrueAndAssignsUse()
    {
        var uses = new Use[] { new(null!, 3) };
        var collection = new UseCollection(null!, uses);

        Assert.True(collection.TryGetSingleUse(out var use));
        Assert.Equal(3, use.Index);
    }

    [Fact]
    public void TryGetSingleUse_MultipleUses_ReturnsFalse()
    {
        var uses = new Use[] { new(null!, 0), new(null!, 1) };
        var collection = new UseCollection(null!, uses);

        Assert.False(collection.TryGetSingleUse(out _));
    }

    [Fact]
    public void TryGetSingleUse_PredicateFiltersToSingleUse_ReturnsTrue()
    {
        var uses = new Use[] { new(null!, 0), new(null!, 1), new(null!, 2) };
        var collection = new UseCollection(null!, uses, use => use.Index == 1);

        Assert.True(collection.TryGetSingleUse(out var use));
        Assert.Equal(1, use.Index);
    }

    #endregion

    #region ILInstruction stack behavior

    [Fact]
    public void GetPushPopBehavior_Rethrow_PopsAndPushesNothing()
    {
        var instruction = new ILInstruction(
            offset: 0,
            ILInstructionType.Rethrow,
            default,
            argument: null);

        var (popCount, pushCount) = instruction.GetPushPopBehavior();
        Assert.Equal(0, popCount);
        Assert.Equal(0, pushCount);
    }

    [Fact]
    public void GetPushPopBehavior_Throw_PopsException()
    {
        var instruction = new ILInstruction(
            offset: 0,
            ILInstructionType.Throw,
            default,
            argument: null);

        var (popCount, pushCount) = instruction.GetPushPopBehavior();
        Assert.Equal(1, popCount);
        Assert.Equal(0, pushCount);
    }

    /// <summary>
    /// The leave at the end of the try block is directly followed by the
    /// catch handler, which starts by popping the exception object.
    /// </summary>
    private static int CatchAndDiscard(int[] values)
    {
        int result = 0;
        try
        {
            result = values[0];
        }
        catch (IndexOutOfRangeException)
        {
        }
        return result;
    }

    /// <summary>
    /// A leave is an unconditional branch: treating the next instruction
    /// (the catch handler) as a successor entered it with the exception
    /// object missing and failed the "Invalid stack counter" assertion.
    /// </summary>
    [Fact]
    public void Disassemble_Leave_HasSingleTarget()
    {
        var method = typeof(CompilerInternalsTests).GetMethod(
            nameof(CatchAndDiscard),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var disassembled = Disassembler.TryDisassemble(method);
        Assert.NotNull(disassembled);

        int numLeaves = 0;
        foreach (var instruction in disassembled.Instructions)
        {
            if (instruction.InstructionType != ILInstructionType.Leave)
                continue;
            var targets = instruction.GetArgumentAs<ILInstructionBranchTargets>();
            Assert.Equal(1, targets.Count);
            ++numLeaves;
        }
        Assert.True(numLeaves >= 2);
    }

    #endregion

    #region AnalysisValue.ToString

    [Fact]
    public void AnalysisValue_ToString_Scalar_PrintsDataOnly()
    {
        var value = new AnalysisValue<int>(42);
        Assert.Equal("42", value.ToString());
    }

    [Fact]
    public void AnalysisValue_ToString_Structure_PrintsDataAndFields()
    {
        var value = new AnalysisValue<int>(42, new[] { 1, 2, 3 });
        Assert.Equal("42 [1, 2, 3]", value.ToString());
    }

    #endregion

    #region Module.GetPrimitiveType

    /// <summary>
    /// A minimal kernel to obtain a sealed module.
    /// </summary>
    private static void PrimitiveTypeKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data) =>
        data[index] = index;

    private static ILGPUC.IR.ModuleValues.Module CompilePrimitiveTypeKernel(
        CompilationProperties properties)
    {
        using var helper = new CompilationHelper();
        var kernel = typeof(CompilerInternalsTests).GetMethod(
            nameof(PrimitiveTypeKernel),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return helper.CompileToModule(kernel, properties);
    }

    [Fact]
    public void Module_GetPrimitiveType_ResolvesEveryBasicValueType()
    {
        var module = CompilePrimitiveTypeKernel(new CompilationProperties());
        foreach (var type in Enum.GetValues<BasicValueType>())
        {
            var primitiveType = module.GetPrimitiveType(type);
            Assert.Equal(type, primitiveType.BasicValueType);
        }
    }

    [Fact]
    public void Module_GetPrimitiveType_Fast32BitOnly_AliasesFloat64()
    {
        var module = CompilePrimitiveTypeKernel(
            new CompilationProperties(MathMode: MathMode.Fast32BitOnly));
        Assert.Equal(
            BasicValueType.Float32,
            module.GetPrimitiveType(BasicValueType.Float64).BasicValueType);
    }

    #endregion

    #region TypeInformationManager class types

    public sealed class Pair
    {
        public int A;
        public int B;
    }

    public sealed class PairHolder
    {
        public Pair? First;
        public int Second;
    }

    public struct PairStruct
    {
        public Pair? First;
        public long Second;
    }

    /// <summary>
    /// Values of class types are pointers occupying a single field, while the
    /// fields of the classes themselves still describe their layouts. Fields of
    /// class types were counted as all fields of their classes, which shifted the
    /// indices of the fields behind them.
    /// </summary>
    [Theory]
    [InlineData(typeof(Pair), 1, new[] { 0, 1 })]
    [InlineData(typeof(PairHolder), 1, new[] { 0, 1 })]
    [InlineData(typeof(PairStruct), 2, new[] { 0, 1 })]
    public void TypeInfo_ClassField_OccupiesSingleField(
        Type type,
        int numFlattenedFields,
        int[] fieldOffsets)
    {
        var typeInfo = new TypeInformationManager().GetTypeInfo(type);
        Assert.Equal(numFlattenedFields, typeInfo.NumFlattenedFields);
        Assert.Equal(fieldOffsets, typeInfo.FieldOffsets);
    }

    #endregion

    #region Non-capturing lambda detection

    /// <summary>
    /// A sealed class without instance state that is not a closure.
    /// </summary>
    private sealed class StatelessHelper
    {
        public override string ToString() => nameof(StatelessHelper);
    }

    /// <summary>
    /// Calls a virtual method on <see cref="object"/>, which has no instance
    /// fields or properties.
    /// </summary>
    private static string DescribeObject(object value) => value.ToString()!;

    [Fact]
    public void IsNotCapturingLambda_ObjectMethod_IsInstanceMethod()
    {
        var method = typeof(object).GetMethod(nameof(ToString))!;
        Assert.False(method.IsNotCapturingLambda());
        Assert.Equal(1, method.GetParameterOffset());
    }

    [Fact]
    public void IsNotCapturingLambda_StatelessClassMethod_IsInstanceMethod()
    {
        var method = typeof(StatelessHelper).GetMethod(nameof(ToString))!;
        Assert.Equal(typeof(StatelessHelper), method.DeclaringType);
        Assert.False(method.IsNotCapturingLambda());
        Assert.Equal(1, method.GetParameterOffset());
    }

    [Fact]
    public void IsNotCapturingLambda_NonCapturingLambda_IsDetected()
    {
        Func<int, int> lambda = value => value + 1;
        Assert.False(lambda.Method.IsStatic);
        Assert.True(lambda.Method.IsNotCapturingLambda());
        Assert.Equal(0, lambda.Method.GetParameterOffset());
    }

    [Fact]
    public void IsNotCapturingLambda_CapturingLambda_IsInstanceMethod()
    {
        int offset = Environment.ProcessorCount;
        Func<int, int> lambda = value => value + offset;
        Assert.False(lambda.Method.IsNotCapturingLambda());
        Assert.Equal(1, lambda.Method.GetParameterOffset());
    }

    /// <summary>
    /// A callvirt to Object::ToString() pops the instance: treating object as a
    /// non-capturing lambda left 'this' on the simulated evaluation stack.
    /// </summary>
    [Fact]
    public void GetPushPopBehavior_CallvirtObjectToString_PopsInstance()
    {
        var method = typeof(CompilerInternalsTests).GetMethod(
            nameof(DescribeObject),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var disassembled = Disassembler.TryDisassemble(method);
        Assert.NotNull(disassembled);

        int numCalls = 0;
        foreach (var instruction in disassembled.Instructions)
        {
            if (instruction.InstructionType != ILInstructionType.Callvirt)
                continue;
            var target = instruction.GetArgumentAs<MethodBase>();
            Assert.Equal(typeof(object), target.DeclaringType);
            var (popCount, pushCount) = instruction.GetPushPopBehavior();
            Assert.Equal(1, popCount);
            Assert.Equal(1, pushCount);
            ++numCalls;
        }
        Assert.Equal(1, numCalls);
    }

    #endregion
}
