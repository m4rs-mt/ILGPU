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
using System;
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
            BasicValueType.Float64 => throw DoubleNotSupported(),
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
            ArithmeticBasicValueType.Float64 => throw DoubleNotSupported(),
            ArithmeticBasicValueType.UInt8 => "uchar",
            ArithmeticBasicValueType.UInt16 => "ushort",
            ArithmeticBasicValueType.UInt32 => "uint",
            ArithmeticBasicValueType.UInt64 => "ulong",
            _ => "void"
        };

    /// <inheritdoc/>
    public override bool SupportsFeature(LanguageFeature feature) =>
        feature switch
        {
            // Metal has no generic address space; every pointer is device,
            // threadgroup, thread or constant.
            LanguageFeature.GenericAddressSpace => false,
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

        // Scoped memory fences require Metal 3.2; older language versions
        // fall back to a threadgroup barrier with device and threadgroup
        // memory ordering.
        yield return "#if __METAL_VERSION__ >= 320";
        yield return "#define ILGPU_THREAD_FENCE(scope) atomic_thread_fence(" +
            "mem_flags::mem_device | mem_flags::mem_threadgroup, " +
            "memory_order_seq_cst, scope)";
        yield return "#else";
        yield return "#define ILGPU_THREAD_FENCE(scope) threadgroup_barrier(" +
            "mem_flags::mem_device | mem_flags::mem_threadgroup)";
        yield return "#endif";

        // Strong compare-and-swap returning the previous value. Metal only
        // provides the weak variant, which may fail spuriously and reports
        // success as a bool; retry until the comparison genuinely fails or
        // the exchange succeeds.
        foreach (var space in new[] { "device", "threadgroup" })
        {
            yield return
                $"inline int ilgpu_atomic_cas({space} atomic_int* ptr, " +
                "int compare, int value) {";
            yield return "    int expected = compare;";
            yield return "    while (!atomic_compare_exchange_weak_explicit(" +
                "ptr, &expected, value, memory_order_relaxed, " +
                "memory_order_relaxed) && expected == compare) { }";
            yield return "    return expected;";
            yield return "}";
        }
    }

    /// <summary>
    /// Creates the compile-time error for 64-bit floats, which Metal does not
    /// support.
    /// </summary>
    private static NotSupportedException DoubleNotSupported() =>
        new("Metal does not support 64-bit floating-point values (double); " +
            "use float instead.");

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
    /// <remarks>The thread position attributes are uint.</remarks>
    public override bool HasUnsignedThreadBuiltIns => true;

    /// <inheritdoc/>
    public override string? FormatFieldAlignment(int alignment) =>
        $" __attribute__((aligned({alignment})))";

    /// <inheritdoc/>
    public override bool SkipsIndexParameter => true;

    /// <inheritdoc/>
    /// <remarks>
    /// Metal dispatches exactly the requested number of threads
    /// (<c>dispatchThreads</c> with non-uniform threadgroups), so the global
    /// position comes from <c>thread_position_in_grid</c>. Deriving it from
    /// the threadgroup position and <c>threads_per_threadgroup</c> is wrong
    /// for the last, smaller threadgroup.
    /// </remarks>
    public override string? EmitIndexComputation(
        string typeName,
        string paramName,
        TypeValue indexType,
        EntryPointLaunch launch)
    {
        // KernelIndex (grouped launch) carries {long GridIndex, int GroupIndex}
        // rather than a multi-dimensional thread index. Map the fields
        // directly to Metal's threadgroup / thread-within-threadgroup
        // builtins instead of deriving a global thread coordinate.
        if (launch.HasKernelIndex)
        {
            var lines = new StringBuilder();
            lines.AppendLine($"  {typeName} {paramName};");
            lines.AppendLine(
                $"  {paramName}.Field0 = (long)threadgroup_position_in_grid.x;");
            lines.AppendLine(
                $"  {paramName}.Field1 = (int)thread_position_in_threadgroup.x;");
            return lines.ToString().TrimEnd();
        }

        // For struct index types (Index2D/Index3D), take each field from
        // the corresponding axis of the grid position
        if (indexType is StructureType structType)
        {
            string[] axes = ["x", "y", "z"];
            var lines = new StringBuilder();
            lines.AppendLine($"  {typeName} {paramName};");
            for (int i = 0; i < structType.NumFields && i < 3; i++)
            {
                lines.AppendLine(
                    $"  {paramName}.Field{i} = (int)thread_position_in_grid.{axes[i]};");
            }
            return lines.ToString().TrimEnd();
        }

        // 1D: the linear grid position
        return $"  {typeName} {paramName} = ({typeName})thread_position_in_grid.x;";
    }

    public override bool IsFileScopeAddressSpace(MemoryAddressSpace addressSpace) =>
        addressSpace == MemoryAddressSpace.Constant;

    public override IEnumerable<string> GetKernelBuiltInParameters()
    {
        yield return
            "uint3 thread_position_in_grid [[thread_position_in_grid]]";
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
