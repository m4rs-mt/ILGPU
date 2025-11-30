// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPULanguageConfiguration.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using System.Collections.Generic;

namespace ILGPUC.Backends.CPU;

/// <summary>
/// C# language configuration for array-based vectorized code generation.
/// </summary>
/// <param name="vectorWidth">The vector width in terms of number of lanes.</param>
sealed class CPULanguageConfiguration(int vectorWidth) : LanguageConfiguration
{
    /// <inheritdoc/>
    /// <remarks>
    /// C# doesn't use explicit address space keywords like CUDA/OpenCL.
    /// </remarks>
    public override string GetAddressSpaceKeyword(MemoryAddressSpace addressSpace) =>
        string.Empty;

    /// <inheritdoc/>
    public override string GetPrimitiveTypeName(BasicValueType basicType) =>
        basicType switch
        {
            BasicValueType.Int1 => "bool",
            BasicValueType.Int8 => "byte",
            BasicValueType.Int16 => "short",
            BasicValueType.Int32 => "int",
            BasicValueType.Int64 => "long",
            BasicValueType.Float16 => "Half",
            BasicValueType.Float32 => "float",
            BasicValueType.Float64 => "double",
            _ => "void"
        };

    /// <inheritdoc/>
    public override string GetPrimitiveTypeName(ArithmeticBasicValueType basicType) =>
        basicType switch
        {
            ArithmeticBasicValueType.UInt1 => "bool",
            ArithmeticBasicValueType.Int8 => "sbyte",
            ArithmeticBasicValueType.UInt8 => "byte",
            ArithmeticBasicValueType.Int16 => "short",
            ArithmeticBasicValueType.UInt16 => "ushort",
            ArithmeticBasicValueType.Int32 => "int",
            ArithmeticBasicValueType.UInt32 => "uint",
            ArithmeticBasicValueType.Int64 => "long",
            ArithmeticBasicValueType.UInt64 => "ulong",
            ArithmeticBasicValueType.Float16 => "Half",
            ArithmeticBasicValueType.Float32 => "float",
            ArithmeticBasicValueType.Float64 => "double",
            _ => "void"
        };

    /// <inheritdoc/>
    public override string GetViewTypeName(
        string elementTypeName,
        string addressSpaceKeyword) =>
        $"CPURuntimeView<{elementTypeName}>";

    /// <inheritdoc/>
    public override string FormatNullLiteral(string typeName) =>
        "default";

    /// <inheritdoc/>
    public override string ViewLengthFieldName => "Length";

    /// <summary>
    /// Gets the vector type name for a given element type using T[].
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <returns>The vector type name (e.g., "float[]").</returns>
    public string GetVectorTypeName(BasicValueType elementType) =>
        $"{GetPrimitiveTypeName(elementType)}[]";

    /// <inheritdoc/>
    /// <remarks>
    /// C# doesn't use pointer syntax in vectorized code.
    /// </remarks>
    public override string PointerSyntax => "*";

    /// <inheritdoc/>
    public override bool SupportsFeature(LanguageFeature feature) =>
        feature switch
        {
            LanguageFeature.InlineFunctions => true,
            LanguageFeature.GenericAddressSpace => true,
            LanguageFeature.ConstantAddressSpace => true,
            LanguageFeature.Atomics => true,
            LanguageFeature.Barriers => true,
            LanguageFeature.BroadcastGroupLevel => true,
            _ => false
        };

    /// <inheritdoc/>
    public override IEnumerable<string> GetHeaderIncludes()
    {
        yield return "using System;";
        yield return "using System.Threading;";
        yield return "using System.Diagnostics;";
        yield return "using ILGPU.Runtime.CPU;";
    }

    /// <inheritdoc/>
    /// <remarks>
    /// C# kernels are public static methods.
    /// </remarks>
    public override string KernelAttribute => "public static unsafe";

    /// <inheritdoc/>
    /// <remarks>
    /// Nested struct types in C# need explicit access modifiers. Use
    /// <c>public struct</c> so they're accessible from the generated
    /// Launch method and KernelEntryPoint parameters.
    /// </remarks>
    public override string StructKeyword => "internal struct";

    /// <inheritdoc/>
    public override string StructFieldModifier => "public ";

    /// <inheritdoc/>
    public override string FormatDefaultValue(string typeName) =>
        $"default({typeName})";

    /// <inheritdoc/>
    public override IntrinsicEmitter CreateIntrinsicEmitter() =>
        new CPUIntrinsicEmitter(vectorWidth);

    /// <inheritdoc/>
    /// <remarks>
    /// CPU emits <c>LoadFieldAddress</c> as an explicit pre-declared
    /// variable (vectorized: <c>nint[]</c> via
    /// <c>CPUVectorIntrinsics.OffsetPointers</c>; scalar: a regular
    /// pointer variable). The expression emitter must reference that
    /// declared name rather than inlining a C-style pointer cast.
    /// </remarks>
    public override bool LoadFieldAddressUsesVariable => true;
}
