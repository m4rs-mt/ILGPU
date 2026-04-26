// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLIntrinsicEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.PureValues;
using System;

namespace ILGPUC.Backends.OpenCL;

/// <summary>
/// Intrinsic emitter for OpenCL C with type-specific function selection.
/// OpenCL uses overloaded built-in functions that work for all float types.
/// </summary>
sealed class CLIntrinsicEmitter(CLVendor vendor) : IntrinsicEmitter
{
    #region Mathematical Operations

    // OpenCL math functions are overloaded for all float types

    /// <inheritdoc/>
    public override string EmitSin(string arg, ArithmeticBasicValueType type) => $"sin({arg})";

    /// <inheritdoc/>
    public override string EmitCos(string arg, ArithmeticBasicValueType type) => $"cos({arg})";

    /// <inheritdoc/>
    public override string EmitTan(string arg, ArithmeticBasicValueType type) => $"tan({arg})";

    /// <inheritdoc/>
    public override string EmitAsin(string arg, ArithmeticBasicValueType type) => $"asin({arg})";

    /// <inheritdoc/>
    public override string EmitAcos(string arg, ArithmeticBasicValueType type) => $"acos({arg})";

    /// <inheritdoc/>
    public override string EmitAtan(string arg, ArithmeticBasicValueType type) => $"atan({arg})";

    /// <inheritdoc/>
    public override string EmitAtan2(string y, string x, ArithmeticBasicValueType type) =>
        $"atan2({y}, {x})";

    /// <inheritdoc/>
    public override string EmitSqrt(string arg, ArithmeticBasicValueType type) => $"sqrt({arg})";

    /// <inheritdoc/>
    public override string EmitRsqrt(string arg, ArithmeticBasicValueType type) => $"rsqrt({arg})";

    /// <inheritdoc/>
    public override string EmitExp(string arg, ArithmeticBasicValueType type) => $"exp({arg})";

    /// <inheritdoc/>
    public override string EmitExp2(string arg, ArithmeticBasicValueType type) => $"exp2({arg})";

    /// <inheritdoc/>
    public override string EmitLog(string arg, ArithmeticBasicValueType type) => $"log({arg})";

    /// <inheritdoc/>
    public override string EmitLog2(string arg, ArithmeticBasicValueType type) => $"log2({arg})";

    /// <inheritdoc/>
    public override string EmitLog10(string arg, ArithmeticBasicValueType type) => $"log10({arg})";

    /// <inheritdoc/>
    public override string EmitPow(string x, string y, ArithmeticBasicValueType type) =>
        $"pow({x}, {y})";

    /// <inheritdoc/>
    public override string EmitAbs(string arg, ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Float16 or
            ArithmeticBasicValueType.Float32 or
            ArithmeticBasicValueType.Float64 => $"fabs({arg})",
            _ => $"abs({arg})"
        };

    /// <inheritdoc/>
    public override string EmitMin(
        string arg1,
        string arg2,
        ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Float16 or
            ArithmeticBasicValueType.Float32 or
            ArithmeticBasicValueType.Float64 => $"fmin({arg1}, {arg2})",
            _ => $"min({arg1}, {arg2})"
        };

    /// <inheritdoc/>
    public override string EmitMax(
        string arg1,
        string arg2,
        ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Float16 or
            ArithmeticBasicValueType.Float32 or
            ArithmeticBasicValueType.Float64 => $"fmax({arg1}, {arg2})",
            _ => $"max({arg1}, {arg2})"
        };

    /// <inheritdoc/>
    public override string EmitFloor(string arg, ArithmeticBasicValueType type) => $"floor({arg})";

    /// <inheritdoc/>
    public override string EmitCeil(string arg, ArithmeticBasicValueType type) => $"ceil({arg})";

    /// <inheritdoc/>
    public override string EmitTrunc(string arg, ArithmeticBasicValueType type) => $"trunc({arg})";

    /// <inheritdoc/>
    public override string EmitRound(string arg, ArithmeticBasicValueType type) => $"round({arg})";

    /// <inheritdoc/>
    public override string EmitClamp(
        string value,
        string min,
        string max,
        ArithmeticBasicValueType type) =>
        $"clamp({value}, {min}, {max})";

    /// <inheritdoc/>
    public override string EmitMultiplyAdd(
        string a,
        string b,
        string c,
        ArithmeticBasicValueType type) =>
        $"fma({a}, {b}, {c})";

    #endregion

    #region Atomic Operations

    /// <inheritdoc/>
    public override string EmitAtomicAdd(string ptr, string value, ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomic_add({ptr}, {value})",
            ArithmeticBasicValueType.Int64 => $"atom_add({ptr}, {value})", // 64-bit extension
            _ => throw new NotSupportedException(
                $"Atomic add not supported for type {type} in OpenCL")
        };

    /// <inheritdoc/>
    public override string EmitAtomicExchange(
        string ptr,
        string value,
        ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomic_xchg({ptr}, {value})",
            ArithmeticBasicValueType.Float32 => $"atomic_xchg({ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic exchange not supported for type {type} in OpenCL")
        };

    /// <inheritdoc/>
    public override string EmitAtomicCAS(
        string ptr,
        string compare,
        string value,
        ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomic_cmpxchg({ptr}, {compare}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic CAS not supported for type {type} in OpenCL")
        };

    /// <inheritdoc/>
    public override string EmitAtomicMin(string ptr, string value, ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomic_min({ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic min not supported for type {type} in OpenCL")
        };

    /// <inheritdoc/>
    public override string EmitAtomicMax(string ptr, string value, ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomic_max({ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic max not supported for type {type} in OpenCL")
        };

    /// <inheritdoc/>
    public override string EmitAtomicAnd(string ptr, string value, ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomic_and({ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic AND not supported for type {type} in OpenCL")
        };

    /// <inheritdoc/>
    public override string EmitAtomicOr(string ptr, string value, ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomic_or({ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic OR not supported for type {type} in OpenCL")
        };

    /// <inheritdoc/>
    public override string EmitAtomicXor(string ptr, string value, ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomic_xor({ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic XOR not supported for type {type} in OpenCL")
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
            BasicValueType.Float32 => $"as_int({arg})",
            BasicValueType.Float64 => $"as_long({arg})",
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
            BasicValueType.Float32 => $"as_float({arg})",
            BasicValueType.Float64 => $"as_double({arg})",
            _ => throw new NotSupportedException(
                $"Int-as-float reinterpret not supported for target {targetType}")
        };

    #endregion

    #region Synchronization

    /// <inheritdoc/>
    public override string EmitBarrier() =>
        "barrier(CLK_LOCAL_MEM_FENCE | CLK_GLOBAL_MEM_FENCE)";

    /// <inheritdoc/>
    public override string EmitMemoryFence() =>
        "mem_fence(CLK_LOCAL_MEM_FENCE | CLK_GLOBAL_MEM_FENCE)";

    #endregion

    #region Warp/Wave Primitives

    /// <inheritdoc/>
    public override string EmitShuffle(
        string variable,
        string origin,
        ShuffleKind kind,
        ArithmeticBasicValueType type)
    {
        if (vendor == CLVendor.Intel)
        {
            var intrinsic = kind switch
            {
                ShuffleKind.Generic => "intel_sub_group_shuffle",
                ShuffleKind.Down => "intel_sub_group_shuffle_down",
                ShuffleKind.Up => "intel_sub_group_shuffle_up",
                ShuffleKind.Xor => "intel_sub_group_shuffle_xor",
                _ => throw new ArgumentException($"Unknown shuffle kind: {kind}")
            };
            // Intel down/up take (current, next/prev, delta) — pass variable for both
            return kind is ShuffleKind.Down or ShuffleKind.Up
                ? $"{intrinsic}({variable}, {variable}, {origin})"
                : $"{intrinsic}({variable}, {origin})";
        }
        else
        {
            var intrinsic = kind switch
            {
                ShuffleKind.Generic => "sub_group_shuffle",
                ShuffleKind.Down => "sub_group_shuffle_down",
                ShuffleKind.Up => "sub_group_shuffle_up",
                ShuffleKind.Xor => "sub_group_shuffle_xor",
                _ => throw new ArgumentException($"Unknown shuffle kind: {kind}")
            };
            return $"{intrinsic}({variable}, {origin})";
        }
    }

    /// <inheritdoc/>
    public override string EmitBroadcast(
        string variable,
        string origin,
        BroadcastKind kind,
        ArithmeticBasicValueType type) =>
        // OpenCL supports both sub-group (warp) and work-group level broadcasts
        kind switch
        {
            BroadcastKind.WarpLevel => $"sub_group_broadcast({variable}, {origin})",
            BroadcastKind.GroupLevel => $"work_group_broadcast({variable}, {origin})",
            _ => throw new NotSupportedException("Unknown broadcast kind."),
        };

    /// <inheritdoc/>
    public override string EmitWarpReduce(
        string variable,
        BinaryArithmeticKind operation,
        WarpReduceKind kind,
        ArithmeticBasicValueType type)
    {
        var intrinsic = operation switch
        {
            BinaryArithmeticKind.Add => "sub_group_reduce_add",
            BinaryArithmeticKind.Min => "sub_group_reduce_min",
            BinaryArithmeticKind.Max => "sub_group_reduce_max",
            _ => throw new NotSupportedException(
                $"Warp reduce operation {operation} is not supported on OpenCL")
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
            ? "sub_group_scan_inclusive"
            : "sub_group_scan_exclusive";

        var intrinsic = operation switch
        {
            BinaryArithmeticKind.Add => $"{prefix}_add",
            BinaryArithmeticKind.Min => $"{prefix}_min",
            BinaryArithmeticKind.Max => $"{prefix}_max",
            _ => throw new NotSupportedException(
                $"Warp scan operation {operation} is not supported on OpenCL")
        };

        return $"{intrinsic}({variable})";
    }

    #endregion

    #region Thread/Block Identification

    /// <inheritdoc/>
    public override string EmitLaneIdx(int dimension) =>
        dimension == 0 ? "get_sub_group_local_id()" : "0";

    /// <inheritdoc/>
    public override string EmitWarpIdx(int dimension) =>
        dimension == 0 ? "get_sub_group_id()" : "0";

    /// <inheritdoc/>
    public override string EmitWarpDim(int dimension) =>
        dimension == 0 ? "get_sub_group_size()" : "1";

    /// <inheritdoc/>
    public override string EmitThreadIdx(int dimension) =>
        $"get_local_id({dimension})";

    /// <inheritdoc/>
    public override string EmitBlockIdx(int dimension) =>
        $"get_group_id({dimension})";

    /// <inheritdoc/>
    public override string EmitBlockDim(int dimension) =>
        $"get_local_size({dimension})";

    /// <inheritdoc/>
    public override string EmitGridDim(int dimension) =>
        $"get_num_groups({dimension})";

    #endregion

    #region Debug and Utility

    /// <inheritdoc/>
    public override string EmitPrintf(string format, params string[] args)
    {
        var argList = args.Length > 0 ? ", " + string.Join(", ", args) : "";
        return $"printf(\"{format}\"{argList})";
    }

    /// <inheritdoc/>
    public override string EmitAssert(string condition, string? message = null)
    {
        // OpenCL doesn't have native assert, implement custom version
        return $"if (!({condition})) {{ /* assertion failed: {message} */ }}";
    }

    /// <inheritdoc/>
    public override bool SupportsPrintf => true; // OpenCL 1.2+

    /// <inheritdoc/>
    public override bool SupportsAssert => false; // No native assert

    #endregion

    #region Constant Emission

    /// <inheritdoc/>
    public override string EmitHalfConstant(ushort rawBits) =>
        $"as_half((ushort)0x{rawBits:X4})";

    #endregion
}
