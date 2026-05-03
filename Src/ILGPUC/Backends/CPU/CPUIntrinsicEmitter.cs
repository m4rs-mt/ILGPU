// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUIntrinsicEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.PureValues;
using System;

namespace ILGPUC.Backends.CPU;

/// <summary>
/// CPU intrinsic emitter using CPUMathIntrinsics for vectorized code generation.
/// </summary>
/// <remarks>
/// Generates intrinsic calls using CPUMathIntrinsics (backed by TensorPrimitives)
/// for all math operations, and CPUAtomicIntrinsics/CPUVectorIntrinsics for
/// atomics and shuffle/broadcast operations.
/// </remarks>
/// <param name="vectorWidth">The current vector width.</param>
sealed class CPUIntrinsicEmitter(int? vectorWidth) : IntrinsicEmitter
{
    #region Mathematical Operations

    /// <summary>
    /// Returns the C# math class for scalar emission: <c>MathF</c> for
    /// <see cref="ArithmeticBasicValueType.Float32"/>, <c>Math</c> otherwise.
    /// </summary>
    /// <remarks>
    /// These Emit* methods are called from the shared
    /// <see cref="ExpressionEmitter"/> for inline scalar expressions only —
    /// vectorized paths go through <see cref="CPUExpressionEmitter.EmitUnaryOp"/>.
    /// </remarks>
    private static string M(ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float32 ? "MathF" : "Math";

    /// <inheritdoc/>
    public override string EmitSin(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Sin({arg})";

    /// <inheritdoc/>
    public override string EmitCos(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Cos({arg})";

    /// <inheritdoc/>
    public override string EmitTan(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Tan({arg})";

    /// <inheritdoc/>
    public override string EmitAsin(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Asin({arg})";

    /// <inheritdoc/>
    public override string EmitAcos(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Acos({arg})";

    /// <inheritdoc/>
    public override string EmitAtan(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Atan({arg})";

    /// <inheritdoc/>
    public override string EmitAtan2(string y, string x, ArithmeticBasicValueType type) =>
        $"{M(type)}.Atan2({y}, {x})";

    /// <inheritdoc/>
    public override string EmitSqrt(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Sqrt({arg})";

    /// <inheritdoc/>
    public override string EmitRsqrt(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float32
            ? $"(1.0f / MathF.Sqrt({arg}))"
            : $"(1.0 / Math.Sqrt({arg}))";

    /// <inheritdoc/>
    public override string EmitExp(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Exp({arg})";

    /// <inheritdoc/>
    public override string EmitExp2(string arg, ArithmeticBasicValueType type) =>
        type == ArithmeticBasicValueType.Float32
            ? $"MathF.Pow(2.0f, {arg})"
            : $"Math.Pow(2.0, {arg})";

    /// <inheritdoc/>
    public override string EmitLog(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Log({arg})";

    /// <inheritdoc/>
    public override string EmitLog2(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Log2({arg})";

    /// <inheritdoc/>
    public override string EmitLog10(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Log10({arg})";

    /// <inheritdoc/>
    public override string EmitPow(string x, string y, ArithmeticBasicValueType type) =>
        $"{M(type)}.Pow({x}, {y})";

    /// <inheritdoc/>
    public override string EmitAbs(string arg, ArithmeticBasicValueType type) =>
        $"Math.Abs({arg})";

    /// <inheritdoc/>
    public override string EmitMin(string arg1, string arg2, ArithmeticBasicValueType type) =>
        $"Math.Min({arg1}, {arg2})";

    /// <inheritdoc/>
    public override string EmitMax(string arg1, string arg2, ArithmeticBasicValueType type) =>
        $"Math.Max({arg1}, {arg2})";

    /// <inheritdoc/>
    public override string EmitFloor(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Floor({arg})";

    /// <inheritdoc/>
    public override string EmitCeil(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Ceiling({arg})";

    /// <inheritdoc/>
    public override string EmitTrunc(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Truncate({arg})";

    /// <inheritdoc/>
    public override string EmitRound(string arg, ArithmeticBasicValueType type) =>
        $"{M(type)}.Round({arg})";

    /// <inheritdoc/>
    public override string EmitClamp(
        string value,
        string min,
        string max,
        ArithmeticBasicValueType type) =>
        $"Math.Clamp({value}, {min}, {max})";

    /// <inheritdoc/>
    public override string EmitMultiplyAdd(
        string a,
        string b,
        string c,
        ArithmeticBasicValueType type) =>
        $"({a} * {b} + {c})";

    #endregion

    #region Atomic Operations

    /// <inheritdoc/>
    /// <remarks>
    /// <paramref name="ptr"/> is a <c>Tensor&lt;nint&gt;</c> of per-lane target
    /// addresses. The active mask suppresses side effects for inactive lanes.
    /// </remarks>
    public override string EmitAtomicAdd(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        $"CPUAtomicIntrinsics.Add({ptr}, {value}, {CPUExpressionEmitter.ActiveMaskName})";

    /// <inheritdoc/>
    public override string EmitAtomicExchange(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        $"CPUAtomicIntrinsics.Exchange({ptr}, {value}, {CPUExpressionEmitter.ActiveMaskName})";

    /// <inheritdoc/>
    public override string EmitAtomicCAS(
        string ptr,
        string compare,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        $"CPUAtomicIntrinsics.CompareExchange({ptr}, {value}, {compare}, {CPUExpressionEmitter.ActiveMaskName})";

    /// <inheritdoc/>
    public override string EmitAtomicMin(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        $"CPUAtomicIntrinsics.Min({ptr}, {value}, {CPUExpressionEmitter.ActiveMaskName})";

    /// <inheritdoc/>
    public override string EmitAtomicMax(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        $"CPUAtomicIntrinsics.Max({ptr}, {value}, {CPUExpressionEmitter.ActiveMaskName})";

    /// <inheritdoc/>
    public override string EmitAtomicAnd(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        $"CPUAtomicIntrinsics.And({ptr}, {value}, {CPUExpressionEmitter.ActiveMaskName})";

    /// <inheritdoc/>
    public override string EmitAtomicOr(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        $"CPUAtomicIntrinsics.Or({ptr}, {value}, {CPUExpressionEmitter.ActiveMaskName})";

    /// <inheritdoc/>
    public override string EmitAtomicXor(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace) =>
        $"CPUAtomicIntrinsics.Xor({ptr}, {value}, {CPUExpressionEmitter.ActiveMaskName})";

    #endregion

    #region Bit-Reinterpret Casts

    // The CPU backend handles FloatAsIntCast / IntAsFloatCast via
    // CPUExpressionEmitter.EmitFloatAsIntCast / EmitIntAsFloatCast and never
    // routes through the shared ExpressionEmitter dispatch, so these are
    // unreachable. Kept abstract-conforming with a clear failure mode.

    /// <inheritdoc/>
    public override string EmitFloatAsInt(
        string arg,
        BasicValueType sourceType,
        BasicValueType targetType) =>
        throw new NotSupportedException(
            "CPU backend uses CPUExpressionEmitter.EmitFloatAsIntCast directly; " +
            "this path should not be reached.");

    /// <inheritdoc/>
    public override string EmitIntAsFloat(
        string arg,
        BasicValueType sourceType,
        BasicValueType targetType) =>
        throw new NotSupportedException(
            "CPU backend uses CPUExpressionEmitter.EmitIntAsFloatCast directly; " +
            "this path should not be reached.");

    #endregion

    #region Synchronization

    /// <inheritdoc/>
    /// <remarks>
    /// Barriers are no-ops in single-threaded vectorized execution.
    /// All lanes execute in lockstep.
    /// </remarks>
    public override string EmitBarrier() =>
        "/* barrier - vectorized execution, no synchronization needed */";

    /// <inheritdoc/>
    public override string EmitMemoryFence() =>
        "System.Threading.Thread.MemoryBarrier()";

    #endregion

    #region Warp/Wave Primitives

    /// <inheritdoc/>
    /// <remarks>
    /// Shuffle operations are simulated using runtime helper methods.
    /// </remarks>
    public override string EmitShuffle(
        string variable,
        string origin,
        ShuffleKind kind,
        ArithmeticBasicValueType type)
    {
        var methodName = kind switch
        {
            ShuffleKind.Generic => "Shuffle",
            ShuffleKind.Down => "ShuffleDown",
            ShuffleKind.Up => "ShuffleUp",
            ShuffleKind.Xor => "ShuffleXor",
            _ => throw new ArgumentException($"Unknown shuffle kind: {kind}")
        };

        var elemType = GetPrimitiveTypeName(type);
        return $"CPUVectorIntrinsics.{methodName}<{elemType}>({variable}, {origin})";
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Broadcast operations are simulated using runtime helper methods.
    /// </remarks>
    public override string EmitBroadcast(
        string variable,
        string origin,
        BroadcastKind kind,
        ArithmeticBasicValueType type)
    {
        var methodName = kind switch
        {
            BroadcastKind.WarpLevel => "Broadcast",
            BroadcastKind.GroupLevel => "BroadcastGroup",
            _ => throw new ArgumentException($"Unknown broadcast kind: {kind}")
        };

        var elemType = GetPrimitiveTypeName(type);
        return $"CPUVectorIntrinsics.{methodName}<{elemType}>({variable}, {origin})";
    }

    /// <summary>
    /// Maps an <see cref="ArithmeticBasicValueType"/> to its C# primitive type
    /// name so generic helpers can be emitted with explicit type arguments.
    /// Roslyn cannot infer <c>T</c> from ReadOnlySpan-backed call sites.
    /// </summary>
    private static string GetPrimitiveTypeName(ArithmeticBasicValueType type) =>
        type switch
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
            _ => throw new ArgumentException(
                $"Unsupported type for shuffle/broadcast: {type}")
        };

    /// <inheritdoc/>
    /// <remarks>
    /// CPU vectorized execution requires respecting <c>activeMask</c> so
    /// that partial SIMD groups (from launch extents not divisible by the
    /// vector width) don't pollute the reduction with tail-lane garbage.
    /// Delegates to <c>CPUVectorIntrinsics.WarpReduce*</c> which takes the
    /// active mask and applies the identity to inactive lanes.
    /// </remarks>
    public override string EmitWarpReduce(
        string variable,
        BinaryArithmeticKind operation,
        WarpReduceKind kind,
        ArithmeticBasicValueType type)
    {
        var methodName = operation switch
        {
            BinaryArithmeticKind.Add => "WarpReduceAdd",
            BinaryArithmeticKind.Mul => "WarpReduceMul",
            BinaryArithmeticKind.Min => "WarpReduceMin",
            BinaryArithmeticKind.Max => "WarpReduceMax",
            BinaryArithmeticKind.And => "WarpReduceAnd",
            BinaryArithmeticKind.Or => "WarpReduceOr",
            BinaryArithmeticKind.Xor => "WarpReduceXor",
            _ => throw new NotSupportedException(
                $"CPU warp reduce does not support op {operation}")
        };
        var elemType = GetPrimitiveTypeName(type);
        return
            $"CPUVectorIntrinsics.{methodName}<{elemType}>(" +
            $"{variable}, {CPUExpressionEmitter.ActiveMaskName})";
    }

    /// <inheritdoc/>
    /// <remarks>
    /// CPU masked warp scan — see <see cref="EmitWarpReduce"/> for rationale.
    /// Only Add and Mul are currently provided.
    /// </remarks>
    public override string EmitWarpScan(
        string variable,
        BinaryArithmeticKind operation,
        WarpScanKind kind,
        ArithmeticBasicValueType type)
    {
        var inclusive = kind == WarpScanKind.Inclusive;
        var methodName = operation switch
        {
            BinaryArithmeticKind.Add => inclusive
                ? "WarpScanInclusiveAdd"
                : "WarpScanExclusiveAdd",
            BinaryArithmeticKind.Mul => inclusive
                ? "WarpScanInclusiveMul"
                : "WarpScanExclusiveMul",
            _ => throw new NotSupportedException(
                $"CPU warp scan does not support op {operation}")
        };
        var elemType = GetPrimitiveTypeName(type);
        return
            $"CPUVectorIntrinsics.{methodName}<{elemType}>(" +
            $"{variable}, {CPUExpressionEmitter.ActiveMaskName})";
    }

    #endregion

    #region Thread/Block Identification

    /// <inheritdoc/>
    /// <remarks>
    /// Lane index within the vectorized execution (0 to vectorWidth-1).
    /// </remarks>
    public override string EmitLaneIdx(int dimension) =>
        dimension == 0 ? "laneIdx" : "0";

    /// <inheritdoc/>
    /// <remarks>
    /// In vectorized execution, warp index is always 0 (single warp).
    /// </remarks>
    public override string EmitWarpIdx(int dimension) => "0";

    /// <inheritdoc/>
    /// <remarks>
    /// Warp dimension is the vector width for dimension 0, 1 otherwise.
    /// </remarks>
    public override string EmitWarpDim(int dimension) =>
        dimension == 0 ? vectorWidth?.ToString() ?? "1" : "1";

    /// <inheritdoc/>
    /// <remarks>
    /// Thread index is the same as lane index in vectorized execution.
    /// </remarks>
    public override string EmitThreadIdx(int dimension) =>
        dimension == 0 ? "laneIdx" : "0";

    /// <inheritdoc/>
    /// <remarks>
    /// Block index is always 0 in vectorized single-block execution.
    /// </remarks>
    public override string EmitBlockIdx(int dimension) => "0";

    /// <inheritdoc/>
    /// <remarks>
    /// Block dimension is the vector width for dimension 0, 1 otherwise.
    /// </remarks>
    public override string EmitBlockDim(int dimension) =>
        dimension == 0 ? vectorWidth?.ToString() ?? "1" : "1";

    /// <inheritdoc/>
    /// <remarks>
    /// Grid dimension is always 1 in vectorized execution.
    /// </remarks>
    public override string EmitGridDim(int dimension) => "1";

    #endregion

    #region Debug and Utility

    /// <inheritdoc/>
    /// <remarks>
    /// Printf is not supported in C# vectorized backend.
    /// </remarks>
    public override string EmitPrintf(string format, params string[] args) =>
        throw new NotSupportedException(
            "Printf is not supported in C# vectorized backend. " +
            "Use Debug.WriteLine or Console.WriteLine instead.");

    /// <inheritdoc/>
    public override string EmitAssert(string condition, string? message = null) =>
        message != null
            ? $"Debug.Assert({condition}, \"{message}\")"
            : $"Debug.Assert({condition})";

    /// <inheritdoc/>
    public override bool SupportsPrintf => false;

    /// <inheritdoc/>
    public override bool SupportsAssert => true;

    #endregion

    #region Constant Emission

    /// <inheritdoc/>
    public override string EmitHalfConstant(ushort rawBits) =>
        $"BitConverter.UInt16BitsToHalf(0x{rawBits:X4})";

    #endregion
}
