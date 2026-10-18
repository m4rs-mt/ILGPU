// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPULanguageConfiguration.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.PureValues;
using System;
using System.Globalization;
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
            BasicValueType.Float16 => "System.Half",
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
            ArithmeticBasicValueType.Float16 => "System.Half",
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
    /// <remarks>
    /// Small integer constants are cast to their storage types (Int8 values
    /// are stored as byte) and floating-point special values use the C#
    /// constants.
    /// </remarks>
    public override string? FormatPrimitive(PrimitiveValue value) =>
        value.BasicValueType switch
        {
            BasicValueType.Int1 => value.Int1Value ? "true" : "false",
            BasicValueType.Int8 =>
                $"((byte){unchecked((byte)value.Int8Value)})",
            BasicValueType.Int16 => $"((short){value.Int16Value})",
            BasicValueType.Int32 => value.Int32Value.ToString(
                CultureInfo.InvariantCulture),
            BasicValueType.Int64 => value.Int64Value.ToString(
                CultureInfo.InvariantCulture) + "L",
            BasicValueType.Float32 => FormatFloat(value.Float32Value),
            BasicValueType.Float64 => FormatDouble(value.Float64Value),
            _ => null
        };

    /// <summary>
    /// Formats a float constant as a round-trippable C# literal.
    /// </summary>
    private static string FormatFloat(float value)
    {
        if (float.IsNaN(value))
            return "float.NaN";
        if (float.IsInfinity(value))
            return value > 0 ? "float.PositiveInfinity" : "float.NegativeInfinity";
        return value.ToString("R", CultureInfo.InvariantCulture) + "f";
    }

    /// <summary>
    /// Formats a double constant as a round-trippable C# literal.
    /// </summary>
    private static string FormatDouble(double value)
    {
        if (double.IsNaN(value))
            return "double.NaN";
        if (double.IsInfinity(value))
        {
            return value > 0
                ? "double.PositiveInfinity"
                : "double.NegativeInfinity";
        }
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return text.AsSpan().IndexOfAny('.', 'E', 'e') >= 0 ? text : text + ".0";
    }

    /// <inheritdoc/>
    public override bool SupportsExplicitFieldOffsets => true;

    /// <inheritdoc/>
    public override string PointerStorageTypeName => "nint";

    /// <inheritdoc/>
    public override string FormatNullLiteral(string typeName) =>
        "default";

    /// <inheritdoc/>
    public override string FormatNullPointer(string typeName) =>
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
    public override bool SupportsFeature(LanguageFeature feature) =>
        feature switch
        {
            LanguageFeature.InlineFunctions => true,
            LanguageFeature.GenericAddressSpace => true,
            LanguageFeature.ConstantAddressSpace => true,
            LanguageFeature.Atomics => true,
            LanguageFeature.Barriers => true,
            LanguageFeature.BroadcastGroupLevel => true,
            LanguageFeature.Recursion => true,
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
    /// Launch method and KernelEntryPoint parameters. Structures are unsafe,
    /// as their fields may be pointers.
    /// </remarks>
    public override string StructKeyword => "internal unsafe struct";

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

    /// <inheritdoc/>
    /// <remarks>
    /// The entry point declares local and shared memory, and passes it on to
    /// the helper methods using it.
    /// </remarks>
    public override bool IsFileScopeAddressSpace(MemoryAddressSpace addressSpace) =>
        addressSpace is not (MemoryAddressSpace.Local or MemoryAddressSpace.Shared);
}
