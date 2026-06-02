// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmIntrinsicEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.PureValues;
using System;

namespace ILGPUC.Backends.ROCm;

/// <summary>
/// Intrinsic emitter for AMD HIP (Heterogeneous-Compute Interface for Portability).
/// HIP is CUDA-compatible with minor keyword differences. Most intrinsics are identical to CUDA.
/// </summary>
sealed class ROCmIntrinsicEmitter : IntrinsicEmitter
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
    public override string? EmitFloatRem(
        string x,
        string y,
        ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float64 ? $"fmod({x}, {y})" : $"fmodf({x}, {y})";

    /// <inheritdoc/>
    public override string EmitPopCount(string arg, ArithmeticBasicValueType type) =>
        Is64BitInteger(type) ? $"__popcll({arg})" : $"__popc({arg})";

    /// <inheritdoc/>
    public override string EmitLeadingZeroCount(
        string arg,
        ArithmeticBasicValueType type) =>
        Is64BitInteger(type) ? $"__clzll({arg})" : $"__clz({arg})";

    /// <inheritdoc/>
    /// <remarks>
    /// There is no trailing-zero intrinsic; <c>__ffs</c> returns the 1-based
    /// position of the lowest set bit, or 0 for zero.
    /// </remarks>
    public override string EmitTrailingZeroCount(
        string arg,
        ArithmeticBasicValueType type) =>
        Is64BitInteger(type)
            ? $"({arg} == 0 ? 64 : __ffsll({arg}) - 1)"
            : $"({arg} == 0 ? 32 : __ffs({arg}) - 1)";

    /// <inheritdoc/>
    public override string EmitAbs(string arg, ArithmeticBasicValueType type) => type switch
    {
        ArithmeticBasicValueType.Float64 => $"fabs({arg})",
        ArithmeticBasicValueType.Float32 => $"fabsf({arg})",
        ArithmeticBasicValueType.Float16 => $"__habs({arg})",
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
            ArithmeticBasicValueType.Int64 => EmitSelect(arg1, "<", arg2),
            ArithmeticBasicValueType.UInt8 or
            ArithmeticBasicValueType.UInt16 or
            ArithmeticBasicValueType.UInt32 or
            ArithmeticBasicValueType.UInt64 =>
                EmitSelect(arg1, "<", arg2, GetUnsignedTypeName(type)),
            _ => $"min({arg1}, {arg2})"
        };

    /// <inheritdoc/>
    public override string EmitMax(string arg1, string arg2, ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.Float64 => $"fmax({arg1}, {arg2})",
            ArithmeticBasicValueType.Float32 => $"fmaxf({arg1}, {arg2})",
            ArithmeticBasicValueType.Int64 => EmitSelect(arg1, ">", arg2),
            ArithmeticBasicValueType.UInt8 or
            ArithmeticBasicValueType.UInt16 or
            ArithmeticBasicValueType.UInt32 or
            ArithmeticBasicValueType.UInt64 =>
                EmitSelect(arg1, ">", arg2, GetUnsignedTypeName(type)),
            _ => $"max({arg1}, {arg2})"
        };

    /// <summary>
    /// Emits a min/max as a conditional expression. HIP has no
    /// <c>llmin</c>/<c>ullmin</c>/<c>umin</c> helpers (those exist only in
    /// CUDA's headers), and its <c>min</c>/<c>max</c> overloads are not
    /// guaranteed for all 64-bit and unsigned types.
    /// </summary>
    /// <remarks>
    /// Unsigned values are stored in signed variables, so unsigned
    /// comparisons cast both operands to <paramref name="compareType"/>.
    /// </remarks>
    private static string EmitSelect(
        string arg1,
        string comparison,
        string arg2,
        string? compareType = null)
    {
        var cast = compareType is null ? string.Empty : $"({compareType})";
        return $"({cast}({arg1}) {comparison} {cast}({arg2}) ? ({arg1}) : ({arg2}))";
    }

    /// <summary>
    /// Returns the HIP name of the given unsigned integer type.
    /// </summary>
    private static string GetUnsignedTypeName(ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.UInt8 => "unsigned char",
            ArithmeticBasicValueType.UInt16 => "unsigned short",
            ArithmeticBasicValueType.UInt32 => "unsigned int",
            _ => "unsigned long long",
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

    // Unsigned values are stored in signed variables; the bitwise atomics
    // operate on the same bits for both. Min/max select the unsigned
    // overloads explicitly. Atomics without a native instruction (e.g. float
    // min/max) are lowered to compare-and-swap loops before code generation.

    /// <inheritdoc/>
    public override string EmitAtomicAdd(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        type switch
        {
            ArithmeticBasicValueType.Float32 => $"atomicAdd((float*)({ptr}), {value})",
            ArithmeticBasicValueType.Float64 => $"atomicAdd((double*)({ptr}), {value})",
            _ => EmitIntegerAtomic("atomicAdd", ptr, value, type, "add"),
        };

    /// <inheritdoc/>
    public override string EmitAtomicExchange(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        type switch
        {
            ArithmeticBasicValueType.Float32 => $"atomicExch((float*)({ptr}), {value})",
            _ => EmitIntegerAtomic("atomicExch", ptr, value, type, "exchange"),
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
            ArithmeticBasicValueType.Float32 =>
                $"__int_as_float(atomicCAS((int*)({ptr}), " +
                $"__float_as_int({compare}), __float_as_int({value})))",
            ArithmeticBasicValueType.Float64 =>
                "__longlong_as_double((long long)atomicCAS(" +
                $"(unsigned long long*)({ptr}), " +
                $"(unsigned long long)__double_as_longlong({compare}), " +
                $"(unsigned long long)__double_as_longlong({value})))",
            ArithmeticBasicValueType.Int32 or ArithmeticBasicValueType.UInt32 =>
                $"atomicCAS((int*)({ptr}), {compare}, {value})",
            ArithmeticBasicValueType.Int64 or ArithmeticBasicValueType.UInt64 =>
                $"atomicCAS((unsigned long long*)({ptr}), " +
                $"(unsigned long long)({compare}), (unsigned long long)({value}))",
            _ => throw new NotSupportedException(
                $"Atomic compare-exchange is not supported for type {type} in HIP")
        };

    /// <inheritdoc/>
    public override string EmitAtomicMin(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        EmitIntegerAtomic("atomicMin", ptr, value, type, "min", signed: true);

    /// <inheritdoc/>
    public override string EmitAtomicMax(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        EmitIntegerAtomic("atomicMax", ptr, value, type, "max", signed: true);

    /// <inheritdoc/>
    public override string EmitAtomicAnd(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        EmitIntegerAtomic("atomicAnd", ptr, value, type, "and");

    /// <inheritdoc/>
    public override string EmitAtomicOr(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        EmitIntegerAtomic("atomicOr", ptr, value, type, "or");

    /// <inheritdoc/>
    public override string EmitAtomicXor(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        EmitIntegerAtomic("atomicXor", ptr, value, type, "xor");

    /// <summary>
    /// Emits an integer atomic. 64-bit operations use the
    /// <c>unsigned long long</c> overloads, except for signed min/max which
    /// use <c>long long</c>; unsigned 32-bit min/max use <c>unsigned int</c>.
    /// </summary>
    private static string EmitIntegerAtomic(
        string function,
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        string operation,
        bool signed = false)
    {
        var typeName = type switch
        {
            ArithmeticBasicValueType.Int32 => "int",
            ArithmeticBasicValueType.UInt32 => signed ? "unsigned int" : "int",
            ArithmeticBasicValueType.Int64 =>
                signed ? "long long" : "unsigned long long",
            ArithmeticBasicValueType.UInt64 => "unsigned long long",
            _ => throw new NotSupportedException(
                $"Atomic {operation} is not supported for type {type} in HIP")
        };
        return $"{function}(({typeName}*)({ptr}), ({typeName})({value}))";
    }

    #endregion

    #region Bit-Reinterpret Casts

    // HIP exposes the same bit-cast intrinsics as CUDA.

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

    // HIP synchronization is identical to CUDA
    /// <inheritdoc/>
    public override string EmitBarrier() => "__syncthreads()";

    /// <inheritdoc/>
    public override string EmitMemoryFence() => "__threadfence()";

    /// <inheritdoc/>
    /// <remarks>
    /// The lanes of a wavefront execute in lockstep; the wave barrier keeps
    /// the compiler from moving memory operations across it.
    /// </remarks>
    public override string EmitWarpBarrier() => "__builtin_amdgcn_wave_barrier()";

    /// <inheritdoc/>
    public override string EmitMemoryFence(MemoryBarrierKind kind) =>
        kind switch
        {
            MemoryBarrierKind.GroupLevel => "__threadfence_block()",
            MemoryBarrierKind.SystemLevel => "__threadfence_system()",
            _ => "__threadfence()",
        };

    /// <inheritdoc/>
    public override string EmitPredicateBarrier(
        string predicate,
        PredicateBarrierKind kind,
        PredicateBarrierPredicateKind predicateKind) =>
        (kind, predicateKind) switch
        {
            (PredicateBarrierKind.GroupLevel, PredicateBarrierPredicateKind.PopCount) =>
                $"__syncthreads_count({predicate})",
            (PredicateBarrierKind.GroupLevel, PredicateBarrierPredicateKind.And) =>
                $"(__syncthreads_and({predicate}) != 0)",
            (PredicateBarrierKind.GroupLevel, _) =>
                $"(__syncthreads_or({predicate}) != 0)",
            (_, PredicateBarrierPredicateKind.PopCount) =>
                $"(int)__popcll(__ballot({predicate}))",
            (_, PredicateBarrierPredicateKind.And) =>
                $"(__all({predicate}) != 0)",
            _ => $"(__any({predicate}) != 0)",
        };

    #endregion

    #region Warp/Wave Primitives

    /// <inheritdoc/>
    public override string EmitShuffle(
        string variable,
        string origin,
        ShuffleKind kind,
        ArithmeticBasicValueType type)
    {
        // HIP uses __shfl variants (without _sync suffix)
        var intrinsic = kind switch
        {
            ShuffleKind.Generic => "__shfl",
            ShuffleKind.Down => "__shfl_down",
            ShuffleKind.Up => "__shfl_up",
            ShuffleKind.Xor => "__shfl_xor",
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
        // Broadcast is shuffle with all threads reading from the same source
        if (kind == BroadcastKind.WarpLevel)
            return $"__shfl({variable}, {origin})";

        // Group-level broadcast not directly supported
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

    // HIP uses hipThreadIdx_x, hipBlockIdx_x, etc. instead of CUDA's threadIdx.x
    /// <inheritdoc/>
    public override string EmitLaneIdx(int dimension) =>
        dimension == 0 ? "(__lane_id() % warpSize)" : "0";

    /// <inheritdoc/>
    public override string EmitWarpIdx(int dimension) =>
        dimension == 0 ? "(hipThreadIdx_x / warpSize)" : "0";

    /// <inheritdoc/>
    public override string EmitWarpDim(int dimension) =>
        dimension == 0 ? "warpSize" : "1";

    /// <inheritdoc/>
    public override string EmitThreadIdx(int dimension) =>
        $"hipThreadIdx_{GetDimensionChar(dimension)}";

    /// <inheritdoc/>
    public override string EmitBlockIdx(int dimension) =>
        $"hipBlockIdx_{GetDimensionChar(dimension)}";

    /// <inheritdoc/>
    public override string EmitBlockDim(int dimension) =>
        $"hipBlockDim_{GetDimensionChar(dimension)}";

    /// <inheritdoc/>
    public override string EmitGridDim(int dimension) =>
        $"hipGridDim_{GetDimensionChar(dimension)}";

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
        // HIP supports native assert (same as CUDA)
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
