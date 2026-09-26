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
using ILGPUC.IR;
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
    public override string? EmitFloatRem(
        string x,
        string y,
        ArithmeticBasicValueType type) =>
        $"fmod({x}, {y})";

    /// <inheritdoc/>
    public override string EmitPopCount(string arg, ArithmeticBasicValueType type) =>
        $"(int)popcount({arg})";

    /// <inheritdoc/>
    public override string EmitLeadingZeroCount(
        string arg,
        ArithmeticBasicValueType type) =>
        $"(int)clz({arg})";

    /// <inheritdoc/>
    public override string EmitTrailingZeroCount(
        string arg,
        ArithmeticBasicValueType type) =>
        $"(int)ctz({arg})";

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
            ArithmeticBasicValueType.UInt8 or
            ArithmeticBasicValueType.UInt16 or
            ArithmeticBasicValueType.UInt32 or
            ArithmeticBasicValueType.UInt64 =>
                EmitUnsignedCall("min", arg1, arg2, type),
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
            ArithmeticBasicValueType.UInt8 or
            ArithmeticBasicValueType.UInt16 or
            ArithmeticBasicValueType.UInt32 or
            ArithmeticBasicValueType.UInt64 =>
                EmitUnsignedCall("max", arg1, arg2, type),
            _ => $"max({arg1}, {arg2})"
        };

    /// <summary>
    /// Emits a binary integer builtin on unsigned operands. Unsigned values
    /// are stored in signed variables, so both operands are cast to the
    /// unsigned type to select the unsigned overload.
    /// </summary>
    private static string EmitUnsignedCall(
        string function,
        string arg1,
        string arg2,
        ArithmeticBasicValueType type)
    {
        var typeName = type switch
        {
            ArithmeticBasicValueType.UInt8 => "uchar",
            ArithmeticBasicValueType.UInt16 => "ushort",
            ArithmeticBasicValueType.UInt32 => "uint",
            _ => "ulong",
        };
        return $"{function}(({typeName})({arg1}), ({typeName})({arg2}))";
    }

    /// <inheritdoc/>
    public override string EmitFloor(string arg, ArithmeticBasicValueType type) => $"floor({arg})";

    /// <inheritdoc/>
    public override string EmitCeil(string arg, ArithmeticBasicValueType type) => $"ceil({arg})";

    /// <inheritdoc/>
    public override string EmitMultiplyAdd(
        string a,
        string b,
        string c,
        ArithmeticBasicValueType type) =>
        $"fma({a}, {b}, {c})";

    #endregion

    #region Atomic Operations

    // Unsigned values are stored in signed variables; the bitwise atomics
    // operate on the same bits for both. Min/max select the unsigned
    // overloads explicitly. 64-bit atomics use the atom_* built-ins of the
    // cl_khr_int64_*_atomics extensions. Atomics without a native built-in
    // (e.g. float add) are lowered to compare-and-swap loops before code
    // generation.

    /// <inheritdoc/>
    public override string EmitAtomicAdd(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        EmitIntegerAtomic("add", ptr, value, type, addressSpace);

    /// <inheritdoc/>
    public override string EmitAtomicExchange(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        type == ArithmeticBasicValueType.Float32
            ? $"atomic_xchg({AtomicCast("float", addressSpace)}({ptr}), {value})"
            : EmitIntegerAtomic("xchg", ptr, value, type, addressSpace);

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
                $"as_float(atomic_cmpxchg({AtomicCast("int", addressSpace)}({ptr}), " +
                $"as_int({compare}), as_int({value})))",
            ArithmeticBasicValueType.Float64 =>
                $"as_double(atom_cmpxchg({AtomicCast("long", addressSpace)}({ptr}), " +
                $"as_long({compare}), as_long({value})))",
            ArithmeticBasicValueType.Int32 or ArithmeticBasicValueType.UInt32 =>
                $"atomic_cmpxchg({AtomicCast("int", addressSpace)}({ptr}), " +
                $"{compare}, {value})",
            ArithmeticBasicValueType.Int64 or ArithmeticBasicValueType.UInt64 =>
                $"atom_cmpxchg({AtomicCast("long", addressSpace)}({ptr}), " +
                $"{compare}, {value})",
            _ => throw new NotSupportedException(
                $"Atomic compare-exchange is not supported for type {type} in OpenCL")
        };

    /// <inheritdoc/>
    public override string EmitAtomicMin(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        EmitIntegerAtomic("min", ptr, value, type, addressSpace, signed: true);

    /// <inheritdoc/>
    public override string EmitAtomicMax(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        EmitIntegerAtomic("max", ptr, value, type, addressSpace, signed: true);

    /// <inheritdoc/>
    public override string EmitAtomicAnd(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        EmitIntegerAtomic("and", ptr, value, type, addressSpace);

    /// <inheritdoc/>
    public override string EmitAtomicOr(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        EmitIntegerAtomic("or", ptr, value, type, addressSpace);

    /// <inheritdoc/>
    public override string EmitAtomicXor(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        EmitIntegerAtomic("xor", ptr, value, type, addressSpace);

    /// <summary>
    /// Emits an integer atomic built-in: <c>atomic_op</c> for 32-bit and
    /// <c>atom_op</c> for 64-bit operands. Unsigned operands use the unsigned
    /// overloads when the operation is sign-sensitive.
    /// </summary>
    private static string EmitIntegerAtomic(
        string operation,
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace,
        bool signed = false)
    {
        var (prefix, typeName) = type switch
        {
            ArithmeticBasicValueType.Int32 => ("atomic", "int"),
            ArithmeticBasicValueType.UInt32 => ("atomic", signed ? "uint" : "int"),
            ArithmeticBasicValueType.Int64 => ("atom", "long"),
            ArithmeticBasicValueType.UInt64 => ("atom", signed ? "ulong" : "long"),
            _ => throw new NotSupportedException(
                $"Atomic {operation} is not supported for type {type} in OpenCL")
        };
        return $"{prefix}_{operation}({AtomicCast(typeName, addressSpace)}({ptr}), " +
            $"({typeName})({value}))";
    }

    /// <summary>
    /// Returns the pointer cast to the atomic operand type in the address
    /// space of the target (local for shared memory, global for global memory).
    /// Atomics on pointers into any other address space are rejected: casting
    /// a generic pointer to global would be undefined if it points to shared
    /// memory.
    /// </summary>
    private static string AtomicCast(string typeName, MemoryAddressSpace addressSpace)
    {
        var space = addressSpace switch
        {
            MemoryAddressSpace.Shared => "local",
            MemoryAddressSpace.Global => "global",
            _ => throw new NotSupportedException(
                $"Atomic operations on {addressSpace} pointers are not " +
                "supported in OpenCL; the pointer must point to global or " +
                "shared memory")
        };
        return $"(volatile {space} {typeName}*)";
    }

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

    /// <inheritdoc/>
    public override string EmitWarpBarrier() =>
        "sub_group_barrier(CLK_LOCAL_MEM_FENCE | CLK_GLOBAL_MEM_FENCE)";

    /// <inheritdoc/>
    /// <remarks>
    /// System-level fences use the device scope, the widest scope available
    /// without shared virtual memory.
    /// </remarks>
    public override string EmitMemoryFence(MemoryBarrierKind kind) =>
        kind == MemoryBarrierKind.GroupLevel
            ? "atomic_work_item_fence(CLK_LOCAL_MEM_FENCE | CLK_GLOBAL_MEM_FENCE, " +
                "memory_order_seq_cst, memory_scope_work_group)"
            : "atomic_work_item_fence(CLK_GLOBAL_MEM_FENCE, " +
                "memory_order_seq_cst, memory_scope_device)";

    /// <inheritdoc/>
    public override string EmitPredicateBarrier(
        string predicate,
        PredicateBarrierKind kind,
        PredicateBarrierPredicateKind predicateKind)
    {
        var (prefix, barrier) = kind == PredicateBarrierKind.GroupLevel
            ? ("work_group", EmitBarrier())
            : ("sub_group", EmitWarpBarrier());
        var vote = predicateKind switch
        {
            PredicateBarrierPredicateKind.PopCount =>
                $"{prefix}_reduce_add(({predicate}) ? 1 : 0)",
            PredicateBarrierPredicateKind.And =>
                $"({prefix}_all({predicate}) != 0)",
            _ => $"({prefix}_any({predicate}) != 0)",
        };
        return $"({barrier}, {vote})";
    }

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

        return $"{intrinsic}({EmitUnsignedOperand(variable, type)})";
    }

    /// <summary>
    /// Casts the operand of a sub-group function to its unsigned type, if the
    /// function works with an unsigned type: unsigned values are stored in signed
    /// variables, and the minimum and maximum functions select their overload by
    /// the type.
    /// </summary>
    private static string EmitUnsignedOperand(
        string variable,
        ArithmeticBasicValueType type) =>
        type switch
        {
            ArithmeticBasicValueType.UInt8 => $"(uchar)({variable})",
            ArithmeticBasicValueType.UInt16 => $"(ushort)({variable})",
            ArithmeticBasicValueType.UInt32 => $"(uint)({variable})",
            ArithmeticBasicValueType.UInt64 => $"(ulong)({variable})",
            _ => variable
        };

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

        return $"{intrinsic}({EmitUnsignedOperand(variable, type)})";
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

    #region Constant Emission

    /// <inheritdoc/>
    public override string EmitHalfConstant(ushort rawBits) =>
        $"as_half((ushort)0x{rawBits:X4})";

    #endregion
}
