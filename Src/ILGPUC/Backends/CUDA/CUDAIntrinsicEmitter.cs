// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CUDAIntrinsicEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.PureValues;
using System;

namespace ILGPUC.Backends.Cuda;

/// <summary>
/// Intrinsic emitter for NVIDIA CUDA with type-specific function selection.
/// </summary>
sealed class CudaIntrinsicEmitter : IntrinsicEmitter
{
    #region Mathematical Operations

    /// <inheritdoc/>
    public override string EmitSin(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"sin({arg})" : $"sinf({arg})";

    /// <inheritdoc/>
    public override string EmitCos(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"cos({arg})" : $"cosf({arg})";

    /// <inheritdoc/>
    public override string EmitTan(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"tan({arg})" : $"tanf({arg})";

    /// <inheritdoc/>
    public override string EmitAsin(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"asin({arg})" : $"asinf({arg})";

    /// <inheritdoc/>
    public override string EmitAcos(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"acos({arg})" : $"acosf({arg})";

    /// <inheritdoc/>
    public override string EmitAtan(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"atan({arg})" : $"atanf({arg})";

    /// <inheritdoc/>
    public override string EmitAtan2(string y, string x, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"atan2({y}, {x})" : $"atan2f({y}, {x})";

    /// <inheritdoc/>
    public override string EmitSqrt(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"sqrt({arg})" : $"sqrtf({arg})";

    /// <inheritdoc/>
    public override string EmitRsqrt(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"rsqrt({arg})" : $"rsqrtf({arg})";

    /// <inheritdoc/>
    public override string EmitExp(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"exp({arg})" : $"expf({arg})";

    /// <inheritdoc/>
    public override string EmitExp2(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"exp2({arg})" : $"exp2f({arg})";

    /// <inheritdoc/>
    public override string EmitLog(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"log({arg})" : $"logf({arg})";

    /// <inheritdoc/>
    public override string EmitLog2(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"log2({arg})" : $"log2f({arg})";

    /// <inheritdoc/>
    public override string EmitLog10(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"log10({arg})" : $"log10f({arg})";

    /// <inheritdoc/>
    public override string EmitPow(string x, string y, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"pow({x}, {y})" : $"powf({x}, {y})";

    /// <inheritdoc/>
    public override string EmitAbs(string arg, ArithmeticBasicValueType type) => type switch
    {
        ArithmeticBasicValueType.Float64 => $"fabs({arg})",
        ArithmeticBasicValueType.Float32 => $"fabsf({arg})",
        ArithmeticBasicValueType.Int64 => $"llabs({arg})",
        ArithmeticBasicValueType.UInt8 or ArithmeticBasicValueType.UInt16 or
        ArithmeticBasicValueType.UInt32 or ArithmeticBasicValueType.UInt64 => arg,
        _ => $"abs({arg})"
    };

    /// <inheritdoc/>
    public override string EmitMin(string arg1, string arg2, ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Float64 => $"fmin({arg1}, {arg2})",
            ArithmeticBasicValueType.Float32 => $"fminf({arg1}, {arg2})",
            ArithmeticBasicValueType.Int64 => $"llmin({arg1}, {arg2})",
            ArithmeticBasicValueType.UInt64 => $"ullmin({arg1}, {arg2})",
            ArithmeticBasicValueType.UInt32 => $"umin({arg1}, {arg2})",
            _ => $"min({arg1}, {arg2})"
        };

    /// <inheritdoc/>
    public override string EmitMax(string arg1, string arg2, ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Float64 => $"fmax({arg1}, {arg2})",
            ArithmeticBasicValueType.Float32 => $"fmaxf({arg1}, {arg2})",
            ArithmeticBasicValueType.Int64 => $"llmax({arg1}, {arg2})",
            ArithmeticBasicValueType.UInt64 => $"ullmax({arg1}, {arg2})",
            ArithmeticBasicValueType.UInt32 => $"umax({arg1}, {arg2})",
            _ => $"max({arg1}, {arg2})"
        };

    /// <inheritdoc/>
    public override string EmitFloor(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"floor({arg})" : $"floorf({arg})";

    /// <inheritdoc/>
    public override string EmitCeil(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"ceil({arg})" : $"ceilf({arg})";

    /// <inheritdoc/>
    public override string EmitTrunc(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"trunc({arg})" : $"truncf({arg})";

    /// <inheritdoc/>
    public override string EmitRound(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"round({arg})" : $"roundf({arg})";

    /// <inheritdoc/>
    public override string EmitClamp(
        string value,
        string min,
        string max,
        ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64
            ? $"fmin(fmax({value}, {min}), {max})"
            : $"fminf(fmaxf({value}, {min}), {max})";

    /// <inheritdoc/>
    public override string EmitMultiplyAdd(
        string a,
        string b,
        string c,
        ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"fma({a}, {b}, {c})" : $"fmaf({a}, {b}, {c})";

    #endregion

    #region Atomic Operations

    /// <inheritdoc/>
    public override string EmitAtomicAdd(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomicAdd({ptr}, {value})",
            ArithmeticBasicValueType.Int64 => $"atomicAdd((unsigned long long*){ptr}, {value})",
            ArithmeticBasicValueType.Float32 => $"atomicAdd({ptr}, {value})",
            ArithmeticBasicValueType.Float64 => $"atomicAdd((double*){ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic add not supported for type {type}")
        };

    /// <inheritdoc/>
    public override string EmitAtomicExchange(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomicExch({ptr}, {value})",
            ArithmeticBasicValueType.Int64 => $"atomicExch((unsigned long long*){ptr}, {value})",
            ArithmeticBasicValueType.Float32 => $"atomicExch({ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic exchange not supported for type {type}")
        };

    /// <inheritdoc/>
    public override string EmitAtomicCAS(
        string ptr,
        string compare,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomicCAS({ptr}, {compare}, {value})",
            ArithmeticBasicValueType.Int64 =>
                $"atomicCAS((unsigned long long*){ptr}, {compare}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic CAS not supported for type {type}")
        };

    /// <inheritdoc/>
    public override string EmitAtomicMin(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomicMin({ptr}, {value})",
            ArithmeticBasicValueType.Int64 => $"atomicMin((unsigned long long*){ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic min not supported for type {type}")
        };

    /// <inheritdoc/>
    public override string EmitAtomicMax(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomicMax({ptr}, {value})",
            ArithmeticBasicValueType.Int64 => $"atomicMax((unsigned long long*){ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic max not supported for type {type}")
        };

    /// <inheritdoc/>
    public override string EmitAtomicAnd(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomicAnd({ptr}, {value})",
            ArithmeticBasicValueType.Int64 => $"atomicAnd((unsigned long long*){ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic AND not supported for type {type}")
        };

    /// <inheritdoc/>
    public override string EmitAtomicOr(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomicOr({ptr}, {value})",
            ArithmeticBasicValueType.Int64 => $"atomicOr((unsigned long long*){ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic OR not supported for type {type}")
        };

    /// <inheritdoc/>
    public override string EmitAtomicXor(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        type switch
        {
            ArithmeticBasicValueType.Int32 => $"atomicXor({ptr}, {value})",
            ArithmeticBasicValueType.Int64 => $"atomicXor((unsigned long long*){ptr}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic XOR not supported for type {type}")
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
            BasicValueType.Float32 => $"__float_as_int({arg})",
            BasicValueType.Float64 => $"__double_as_longlong({arg})",
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
            BasicValueType.Float32 => $"__int_as_float({arg})",
            BasicValueType.Float64 => $"__longlong_as_double({arg})",
            _ => throw new NotSupportedException(
                $"Int-as-float reinterpret not supported for target {targetType}")
        };

    #endregion

    #region Synchronization

    /// <inheritdoc/>
    public override string EmitBarrier() => "__syncthreads()";

    /// <inheritdoc/>
    public override string EmitMemoryFence() => "__threadfence()";

    #endregion

    #region Warp/Wave Primitives

    /// <inheritdoc/>
    public override string EmitShuffle(
        string variable,
        string origin,
        ShuffleKind kind,
        ArithmeticBasicValueType type)
    {
        // CUDA uses __shfl_sync variants with 0xffffffff as the mask (all lanes active)
        var intrinsic = kind switch
        {
            ShuffleKind.Generic => "__shfl_sync",
            ShuffleKind.Down => "__shfl_down_sync",
            ShuffleKind.Up => "__shfl_up_sync",
            ShuffleKind.Xor => "__shfl_xor_sync",
            _ => throw new ArgumentException($"Unknown shuffle kind: {kind}")
        };

        return $"{intrinsic}(0xffffffff, {variable}, {origin})";
    }

    /// <inheritdoc/>
    public override string EmitBroadcast(
        string variable,
        string origin,
        BroadcastKind kind,
        ArithmeticBasicValueType type)
    {
        // Broadcast is shuffle with all threads reading from the same source
        // For warp-level: use __shfl_sync
        // For group-level: would need shared memory (not commonly supported as intrinsic)
        if (kind == BroadcastKind.WarpLevel)
            return $"__shfl_sync(0xffffffff, {variable}, {origin})";

        // Group-level broadcast not directly supported in CUDA
        throw new NotSupportedException(
            "Group-level broadcast requires shared memory and is not supported as a "
            + "direct intrinsic.");
    }

    /// <inheritdoc/>
    public override string EmitWarpReduce(
        string variable,
        BinaryArithmeticKind operation,
        WarpReduceKind kind,
        ArithmeticBasicValueType type) =>
        throw new NotSupportedException(
            "Warp reduce/scan must be lowered to shuffles before codegen");

    /// <inheritdoc/>
    public override string EmitWarpScan(
        string variable,
        BinaryArithmeticKind operation,
        WarpScanKind kind,
        ArithmeticBasicValueType type) =>
        throw new NotSupportedException(
            "Warp reduce/scan must be lowered to shuffles before codegen");

    #endregion

    #region Thread/Block Identification

    /// <inheritdoc/>
    /// <remarks>
    /// CUDA has no <c>__lane_id()</c> built-in (that is a HIP/ROCm
    /// extension). The portable CUDA expression for the lane index
    /// within a warp is <c>(threadIdx.x &amp; (warpSize - 1))</c>,
    /// which assumes the warp lies on the x dimension — true for every
    /// kernel ILGPU currently supports.
    /// </remarks>
    public override string EmitLaneIdx(int dimension) =>
        dimension == 0 ? "(threadIdx.x & (warpSize - 1))" : "0";

    /// <inheritdoc/>
    public override string EmitWarpIdx(int dimension) =>
        dimension == 0 ? "(threadIdx.x / warpSize)" : "0";

    /// <inheritdoc/>
    public override string EmitWarpDim(int dimension) =>
        dimension == 0 ? "warpSize" : "1";

    /// <inheritdoc/>
    public override string EmitThreadIdx(int dimension) =>
        $"threadIdx.{GetDimensionChar(dimension)}";

    /// <inheritdoc/>
    public override string EmitBlockIdx(int dimension) =>
        $"blockIdx.{GetDimensionChar(dimension)}";

    /// <inheritdoc/>
    public override string EmitBlockDim(int dimension) =>
        $"blockDim.{GetDimensionChar(dimension)}";

    /// <inheritdoc/>
    public override string EmitGridDim(int dimension) =>
        $"gridDim.{GetDimensionChar(dimension)}";

    /// <inheritdoc/>
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
    public override string EmitPrintf(string format, params string[] args)
    {
        var argList = args.Length > 0 ? ", " + string.Join(", ", args) : "";
        return $"printf(\"{format}\"{argList})";
    }

    /// <inheritdoc/>
    public override string EmitAssert(string condition, string? message = null)
    {
        // CUDA supports native assert
        return $"assert({condition})";
    }

    /// <inheritdoc/>
    public override bool SupportsPrintf => true;
    /// <inheritdoc/>
    public override bool SupportsAssert => true;

    #endregion

    #region Constant Emission

    /// <inheritdoc/>
    public override string EmitHalfConstant(ushort rawBits) =>
        $"__ushort_as_half(0x{rawBits:X4})";

    #endregion
}
