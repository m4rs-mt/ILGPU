// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalIntrinsicEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.PureValues;
using System;

namespace ILGPUC.Backends.Metal;

/// <summary>
/// Intrinsic emitter for Apple Metal Shading Language with type-specific function
/// selection. Metal uses the metal:: namespace prefix for most math functions.
/// </summary>
sealed class MetalIntrinsicEmitter : IntrinsicEmitter
{
    #region Mathematical Operations

    // Metal math functions work for all float types (half, float, double)
    // The compiler selects the appropriate overload based on argument types

    /// <inheritdoc/>
    public override string EmitSin(string arg, ArithmeticBasicValueType type) =>
        $"metal::sin({arg})";

    /// <inheritdoc/>
    public override string EmitCos(string arg, ArithmeticBasicValueType type) =>
        $"metal::cos({arg})";

    /// <inheritdoc/>
    public override string EmitTan(string arg, ArithmeticBasicValueType type) =>
        $"metal::tan({arg})";

    /// <inheritdoc/>
    public override string EmitAsin(string arg, ArithmeticBasicValueType type) =>
        $"metal::asin({arg})";

    /// <inheritdoc/>
    public override string EmitAcos(string arg, ArithmeticBasicValueType type) =>
        $"metal::acos({arg})";

    /// <inheritdoc/>
    public override string EmitAtan(string arg, ArithmeticBasicValueType type) =>
        $"metal::atan({arg})";

    /// <inheritdoc/>
    public override string EmitAtan2(string y, string x, ArithmeticBasicValueType type) =>
        $"metal::atan2({y}, {x})";

    /// <inheritdoc/>
    public override string EmitSqrt(string arg, ArithmeticBasicValueType type) =>
        $"metal::sqrt({arg})";

    /// <inheritdoc/>
    public override string EmitRsqrt(string arg, ArithmeticBasicValueType type) =>
        $"metal::rsqrt({arg})";

    /// <inheritdoc/>
    public override string EmitExp(string arg, ArithmeticBasicValueType type) =>
        $"metal::exp({arg})";

    /// <inheritdoc/>
    public override string EmitExp2(string arg, ArithmeticBasicValueType type) =>
        $"metal::exp2({arg})";

    /// <inheritdoc/>
    public override string EmitLog(string arg, ArithmeticBasicValueType type) =>
        $"metal::log({arg})";

    /// <inheritdoc/>
    public override string EmitLog2(string arg, ArithmeticBasicValueType type) =>
        $"metal::log2({arg})";

    /// <inheritdoc/>
    public override string EmitLog10(string arg, ArithmeticBasicValueType type) =>
        $"metal::log10({arg})";

    /// <inheritdoc/>
    public override string EmitPow(string x, string y, ArithmeticBasicValueType type) =>
        $"metal::pow({x}, {y})";

    /// <inheritdoc/>
    public override string EmitAbs(string arg, ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Float32 or
            ArithmeticBasicValueType.Float64 or
            ArithmeticBasicValueType.Float16 => $"metal::abs({arg})",
            _ => $"metal::abs({arg})"
        };

    /// <inheritdoc/>
    public override string EmitMin(string arg1, string arg2, ArithmeticBasicValueType type) =>
        $"metal::min({arg1}, {arg2})";

    /// <inheritdoc/>
    public override string EmitMax(string arg1, string arg2, ArithmeticBasicValueType type) =>
        $"metal::max({arg1}, {arg2})";

    /// <inheritdoc/>
    public override string EmitFloor(string arg, ArithmeticBasicValueType type) =>
        $"metal::floor({arg})";

    /// <inheritdoc/>
    public override string EmitCeil(string arg, ArithmeticBasicValueType type) =>
        $"metal::ceil({arg})";

    /// <inheritdoc/>
    public override string EmitTrunc(string arg, ArithmeticBasicValueType type) =>
        $"metal::trunc({arg})";

    /// <inheritdoc/>
    public override string EmitRound(string arg, ArithmeticBasicValueType type) =>
        $"metal::round({arg})";

    /// <inheritdoc/>
    public override string EmitClamp(
        string value,
        string min,
        string max,
        ArithmeticBasicValueType type) =>
        $"metal::clamp({value}, {min}, {max})";

    /// <inheritdoc/>
    public override string EmitMultiplyAdd(
        string a,
        string b,
        string c,
        ArithmeticBasicValueType type) =>
        $"metal::fma({a}, {b}, {c})";

    #endregion

    #region Atomic Operations

    /// <inheritdoc/>
    public override string EmitAtomicAdd(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        "atomic_fetch_add_explicit(" +
        $"{AtomicCast(addressSpace, type)}{ptr}, {value}, memory_order_relaxed)";

    /// <inheritdoc/>
    public override string EmitAtomicExchange(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        "atomic_exchange_explicit(" +
        $"{AtomicCast(addressSpace, type)}{ptr}, {value}, memory_order_relaxed)";

    /// <inheritdoc/>
    public override string EmitAtomicCAS(
        string ptr,
        string compare,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        "atomic_compare_exchange_weak_explicit(" +
        $"{AtomicCast(addressSpace, type)}{ptr}, &{compare}, {value}, " +
        "memory_order_relaxed, memory_order_relaxed)";

    /// <inheritdoc/>
    public override string EmitAtomicMin(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        "atomic_fetch_min_explicit(" +
        $"{AtomicCast(addressSpace, type)}{ptr}, {value}, memory_order_relaxed)";

    /// <inheritdoc/>
    public override string EmitAtomicMax(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        "atomic_fetch_max_explicit(" +
        $"{AtomicCast(addressSpace, type)}{ptr}, {value}, memory_order_relaxed)";

    /// <inheritdoc/>
    public override string EmitAtomicAnd(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        "atomic_fetch_and_explicit(" +
        $"{AtomicCast(addressSpace, type)}{ptr}, {value}, memory_order_relaxed)";

    /// <inheritdoc/>
    public override string EmitAtomicOr(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        "atomic_fetch_or_explicit(" +
        $"{AtomicCast(addressSpace, type)}{ptr}, {value}, memory_order_relaxed)";

    /// <inheritdoc/>
    public override string EmitAtomicXor(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        "atomic_fetch_xor_explicit(" +
        $"{AtomicCast(addressSpace, type)}{ptr}, {value}, memory_order_relaxed)";

    /// <summary>
    /// Builds the leading address-space-qualified pointer cast used inside
    /// every <c>atomic_*_explicit</c> call. Mirrors
    /// <see cref="MetalLanguageConfiguration.GetAddressSpaceKeyword"/> so
    /// shared-memory atomics emit <c>(threadgroup atomic_int*)</c> instead
    /// of the cross-address-space <c>(device atomic_int*)</c> cast that
    /// Metal rejects.
    /// </summary>
    private static string AtomicCast(
        MemoryAddressSpace addressSpace,
        ArithmeticBasicValueType type) =>
        $"({GetAddressSpaceQualifier(addressSpace)} {GetAtomicType(type)}*)";

    private static string GetAddressSpaceQualifier(MemoryAddressSpace addressSpace) =>
        addressSpace switch
        {
            MemoryAddressSpace.Shared => "threadgroup",
            MemoryAddressSpace.Local => "thread",
            MemoryAddressSpace.Constant => "constant",
            _ => "device",
        };

    /// <inheritdoc/>
    private static string GetAtomicType(ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => "atomic_int",
            ArithmeticBasicValueType.Int64 => "atomic_long",
            ArithmeticBasicValueType.Int8 => "atomic_char",
            ArithmeticBasicValueType.Int16 => "atomic_short",
            _ => throw new NotSupportedException(
                $"Atomic operations not supported for type {type}")
        };

    #endregion

    #region Bit-Reinterpret Casts

    /// <inheritdoc/>
    public override string EmitFloatAsInt(
        string arg,
        BasicValueType sourceType,
        BasicValueType targetType) =>
        sourceType switch
        {
            BasicValueType.Float32 => $"as_type<int>({arg})",
            BasicValueType.Float64 => $"as_type<long>({arg})",
            _ => throw new NotSupportedException(
                $"Float-as-int reinterpret not supported for source {sourceType}")
        };

    /// <inheritdoc/>
    public override string EmitIntAsFloat(
        string arg,
        BasicValueType sourceType,
        BasicValueType targetType) =>
        targetType switch
        {
            BasicValueType.Float32 => $"as_type<float>({arg})",
            BasicValueType.Float64 => $"as_type<double>({arg})",
            _ => throw new NotSupportedException(
                $"Int-as-float reinterpret not supported for target {targetType}")
        };

    #endregion

    #region Synchronization

    /// <inheritdoc/>
    public override string EmitBarrier() =>
        "threadgroup_barrier(mem_flags::mem_threadgroup)";

    /// <inheritdoc/>
    public override string EmitMemoryFence() =>
        "threadgroup_barrier(mem_flags::mem_device)";

    #endregion

    #region Warp/Wave Primitives

    /// <inheritdoc/>
    public override string EmitShuffle(
        string variable,
        string origin,
        ShuffleKind kind,
        ArithmeticBasicValueType type)
    {
        // Metal uses simd_ prefix for SIMD group operations
        var intrinsic = kind switch
        {
            ShuffleKind.Generic => "simd_shuffle",
            ShuffleKind.Down => "simd_shuffle_down",
            ShuffleKind.Up => "simd_shuffle_up",
            ShuffleKind.Xor => "simd_shuffle_xor",
            _ => throw new ArgumentException($"Unknown shuffle kind: {kind}")
        };

        return $"{intrinsic}({variable}, {origin})";
    }

    /// <inheritdoc/>
    public override string EmitBroadcast(
        string variable,
        string origin,
        BroadcastKind kind,
        ArithmeticBasicValueType type)
    {
        // Broadcast at SIMD group (warp) level
        if (kind == BroadcastKind.WarpLevel)
            return $"simd_broadcast({variable}, {origin})";

        // Group-level broadcast not directly supported
        throw new NotSupportedException(
            "Group-level broadcast requires threadgroup memory and is not supported as "
            + "a direct intrinsic.");
    }

    /// <inheritdoc/>
    public override string EmitWarpReduce(
        string variable,
        BinaryArithmeticKind operation,
        WarpReduceKind kind,
        ArithmeticBasicValueType type)
    {
        // Metal simd_ reduce functions are inherently all-reduce (all lanes
        // receive the result), so both Reduce and AllReduce map to the same call.
        var intrinsic = operation switch
        {
            BinaryArithmeticKind.Add => "simd_sum",
            BinaryArithmeticKind.Min => "simd_min",
            BinaryArithmeticKind.Max => "simd_max",
            BinaryArithmeticKind.And => "simd_and",
            BinaryArithmeticKind.Or => "simd_or",
            BinaryArithmeticKind.Xor => "simd_xor",
            BinaryArithmeticKind.Mul => "simd_product",
            _ => throw new NotSupportedException(
                $"Warp reduce operation {operation} is not supported on Metal")
        };

        return $"{intrinsic}({variable})";
    }

    /// <inheritdoc/>
    public override string EmitWarpScan(
        string variable,
        BinaryArithmeticKind operation,
        WarpScanKind kind,
        ArithmeticBasicValueType type)
    {
        var prefix = kind == WarpScanKind.Inclusive
            ? "simd_prefix_inclusive"
            : "simd_prefix_exclusive";

        var intrinsic = operation switch
        {
            BinaryArithmeticKind.Add => $"{prefix}_sum",
            BinaryArithmeticKind.Mul => $"{prefix}_product",
            _ => throw new NotSupportedException(
                $"Warp scan operation {operation} is not supported on Metal")
        };

        return $"{intrinsic}({variable})";
    }

    #endregion

    #region Thread/Block Identification

    /// <inheritdoc/>
    public override string EmitLaneIdx(int dimension) =>
        dimension == 0 ? "thread_index_in_simdgroup" : "0";

    /// <inheritdoc/>
    public override string EmitWarpIdx(int dimension) =>
        dimension == 0 ? "simdgroup_index_in_threadgroup" : "0";

    /// <inheritdoc/>
    public override string EmitWarpDim(int dimension) =>
        dimension == 0 ? "threads_per_simdgroup" : "1";

    /// <inheritdoc/>
    public override string EmitThreadIdx(int dimension) =>
        $"thread_position_in_threadgroup.{GetDimensionChar(dimension)}";

    /// <inheritdoc/>
    public override string EmitBlockIdx(int dimension) =>
        $"threadgroup_position_in_grid.{GetDimensionChar(dimension)}";

    /// <inheritdoc/>
    public override string EmitBlockDim(int dimension) =>
        $"threads_per_threadgroup.{GetDimensionChar(dimension)}";

    /// <inheritdoc/>
    public override string EmitGridDim(int dimension) =>
        $"threadgroups_per_grid.{GetDimensionChar(dimension)}";

    private static char GetDimensionChar(int dimension) => dimension switch
    {
        0 => 'x',
        1 => 'y',
        2 => 'z',
        _ => throw new ArgumentOutOfRangeException(nameof(dimension))
    };

    #endregion

    #region Debug and Utility

    /// <inheritdoc/>
    public override string EmitPrintf(string format, params string[] args) =>
        throw new NotSupportedException("Metal does not support device-side printf. " +
        "Use a device buffer for debug output.");

    /// <inheritdoc/>
    public override string EmitAssert(string condition, string? message = null)
    {
        // Metal doesn't have native assert, implement custom version
        return $"if (!({condition})) {{ /* assertion failed: {message} */ }}";
    }

    /// <inheritdoc/>
    public override bool SupportsPrintf => false;

    /// <inheritdoc/>
    public override bool SupportsAssert => false;

    #endregion

    #region Constant Emission

    /// <inheritdoc/>
    public override string EmitHalfConstant(ushort rawBits) =>
        $"as_type<half>((ushort)0x{rawBits:X4})";

    #endregion
}
