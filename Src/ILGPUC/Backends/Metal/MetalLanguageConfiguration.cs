// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalLanguageConfiguration.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using System.Collections.Generic;
using System.Text;

namespace ILGPUC.Backends.Metal;

/// <summary>
/// Metal-specific language configuration.
/// </summary>
sealed class MetalLanguageConfiguration : LanguageConfiguration
{
    /// <inheritdoc/>
    public override string GetAddressSpaceKeyword(MemoryAddressSpace addressSpace) =>
        addressSpace switch
        {
            MemoryAddressSpace.Generic => "device",
            MemoryAddressSpace.Global => "device",
            MemoryAddressSpace.Shared => "threadgroup",
            MemoryAddressSpace.Local => "thread",
            MemoryAddressSpace.Constant => "constant",
            _ => "device"
        };

    /// <inheritdoc/>
    public override string GetPrimitiveTypeName(BasicValueType basicType) =>
        basicType switch
        {
            BasicValueType.Int1 => "bool",
            BasicValueType.Int8 => "char",
            BasicValueType.Int16 => "short",
            BasicValueType.Int32 => "int",
            BasicValueType.Int64 => "long",
            BasicValueType.Float16 => "half",
            BasicValueType.Float32 => "float",
            BasicValueType.Float64 => "double",
            _ => "void"
        };

    /// <inheritdoc/>
    public override string GetPrimitiveTypeName(ArithmeticBasicValueType basicType) =>
        basicType switch
        {
            ArithmeticBasicValueType.UInt1 => "bool",
            ArithmeticBasicValueType.Int8 => "char",
            ArithmeticBasicValueType.Int16 => "short",
            ArithmeticBasicValueType.Int32 => "int",
            ArithmeticBasicValueType.Int64 => "long",
            ArithmeticBasicValueType.Float16 => "half",
            ArithmeticBasicValueType.Float32 => "float",
            ArithmeticBasicValueType.Float64 => "double",
            ArithmeticBasicValueType.UInt8 => "uchar",
            ArithmeticBasicValueType.UInt16 => "ushort",
            ArithmeticBasicValueType.UInt32 => "uint",
            ArithmeticBasicValueType.UInt64 => "ulong",
            _ => "void"
        };

    /// <inheritdoc/>
    public override string PointerSyntax => "*";

    /// <inheritdoc/>
    public override bool SupportsFeature(LanguageFeature feature) =>
        feature switch
        {
            LanguageFeature.GenericAddressSpace => true,
            LanguageFeature.ConstantAddressSpace => true,
            LanguageFeature.LocalAddressSpace => true,
            LanguageFeature.Atomics => true,
            LanguageFeature.Barriers => true,
            LanguageFeature.InlineFunctions => true,
            _ => false
        };

    /// <inheritdoc/>
    public override IEnumerable<string> GetHeaderIncludes()
    {
        yield return "#include <metal_stdlib>";
        yield return "using namespace metal;";
    }

    /// <inheritdoc/>
    public override string KernelAttribute => "kernel";

    /// <inheritdoc/>
    public override IntrinsicEmitter CreateIntrinsicEmitter() =>
        new MetalIntrinsicEmitter();

    /// <inheritdoc/>
    /// <remarks>
    /// All entry-point parameters need a <c>[[buffer(n)]]</c> attribute in Metal.
    /// Pointer types (device/constant T*) are passed directly; scalar types must be
    /// wrapped as <c>constant T&amp;</c> because Metal does not allow <c>[[buffer]]</c>
    /// on plain scalar parameters.
    /// </remarks>
    public override string EmitNonFlattenedKernelParam(
        TypeValue type,
        string typeName,
        string paramName,
        int bufferIndex) =>
        type is AddressSpaceType
            ? $"{typeName} {paramName} [[buffer({bufferIndex})]]"
            : $"constant {typeName}& {paramName} [[buffer({bufferIndex})]]";

    /// <inheritdoc/>
    /// <remarks>
    /// In Metal, thread position and group index values are not pre-declared globals.
    /// They must be declared as kernel function parameters with [[attribute]] qualifiers.
    /// All four are emitted unconditionally; unused ones are silently ignored by the
    /// Metal compiler.
    /// </remarks>
    /// <inheritdoc/>
    /// <remarks>
    /// In Metal, only <c>constant</c> address space variables may appear at
    /// program scope. <c>thread</c> (local) and <c>threadgroup</c> (shared)
    /// variables must be declared inside a function.
    /// </remarks>
    /// <inheritdoc/>
    public override bool UsesCStyleStructLiterals => true;

    /// <inheritdoc/>
    public override bool SkipsIndexParameter => true;

    /// <inheritdoc/>
    public override string? EmitIndexComputation(
        string typeName, string paramName, TypeValue indexType)
    {
        // KernelIndex (grouped launch) carries {long GridIndex, int GroupIndex}
        // rather than a multi-dimensional thread index. Map the fields
        // directly to Metal's threadgroup / thread-within-threadgroup
        // builtins instead of deriving a global thread coordinate.
        if (indexType is StructureType kernelIdx
            && IsKernelIndexStructShape(kernelIdx))
        {
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"  {typeName} {paramName};");
            lines.AppendLine(
                $"  {paramName}.Field0 = (long)threadgroup_position_in_grid.x;");
            lines.AppendLine(
                $"  {paramName}.Field1 = (int)thread_position_in_threadgroup.x;");
            return lines.ToString().TrimEnd();
        }

        // For struct index types (Index2D/Index3D), compute each field from
        // the corresponding dimension's built-in thread attributes
        if (indexType is StructureType structType)
        {
            string[] axes = ["x", "y", "z"];
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"  {typeName} {paramName};");
            for (int i = 0; i < structType.NumFields && i < 3; i++)
            {
                var axis = axes[i];
                lines.AppendLine($"  {paramName}.Field{i} = (int)(" +
                    $"threadgroup_position_in_grid.{axis} * " +
                    $"threads_per_threadgroup.{axis} + " +
                    $"thread_position_in_threadgroup.{axis});");
            }
            return lines.ToString().TrimEnd();
        }

        // 1D: compute a linear index
        return $"  {typeName} {paramName} = " +
            $"({typeName})(threadgroup_position_in_grid.x * " +
            $"threads_per_threadgroup.x + thread_position_in_threadgroup.x);";
    }

    /// <summary>
    /// Matches the flat field layout of <c>ILGPU.KernelIndex</c> —
    /// <c>{i64 GridIndex, i32 GroupIndex}</c>. Used to decide whether the
    /// first kernel parameter should be filled from Metal's group / thread
    /// builtins or treated as a multi-dim thread coordinate.
    /// </summary>
    private static bool IsKernelIndexStructShape(StructureType st) =>
        st.NumFields == 2
        && !st.HasFlags(TypeFlags.PointerDependent | TypeFlags.ViewDependent)
        && st.Fields[0] is PrimitiveType
            { BasicValueType: BasicValueType.Int64 }
        && st.Fields[1] is PrimitiveType
            { BasicValueType: BasicValueType.Int32 };

    public override bool IsFileScopeAddressSpace(MemoryAddressSpace addressSpace) =>
        addressSpace == MemoryAddressSpace.Constant;

    public override IEnumerable<string> GetKernelBuiltInParameters()
    {
        yield return
            "uint3 thread_position_in_threadgroup [[thread_position_in_threadgroup]]";
        yield return
            "uint3 threadgroup_position_in_grid [[threadgroup_position_in_grid]]";
        yield return
            "uint3 threads_per_threadgroup [[threads_per_threadgroup]]";
        yield return
            "uint3 threadgroups_per_grid [[threadgroups_per_grid]]";
        // SIMD/warp builtins
        yield return
            "uint thread_index_in_simdgroup [[thread_index_in_simdgroup]]";
        yield return
            "uint simdgroup_index_in_threadgroup [[simdgroup_index_in_threadgroup]]";
        yield return
            "uint threads_per_simdgroup [[threads_per_simdgroup]]";
    }
}
