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
    public override string EmitPopCount(string arg, ArithmeticBasicValueType type) =>
        $"System.Numerics.BitOperations.PopCount({AsUnsigned(arg, type)})";

    /// <inheritdoc/>
    public override string EmitLeadingZeroCount(
        string arg,
        ArithmeticBasicValueType type) =>
        $"System.Numerics.BitOperations.LeadingZeroCount({AsUnsigned(arg, type)})";

    /// <inheritdoc/>
    public override string EmitTrailingZeroCount(
        string arg,
        ArithmeticBasicValueType type) =>
        $"System.Numerics.BitOperations.TrailingZeroCount({AsUnsigned(arg, type)})";

    /// <summary>
    /// Reinterprets an integer as the unsigned type of the same width, which
    /// the BitOperations overloads take.
    /// </summary>
    private static string AsUnsigned(string arg, ArithmeticBasicValueType type) =>
        Is64BitInteger(type) ? $"unchecked((ulong)({arg}))" : $"unchecked((uint)({arg}))";

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
    /// <remarks>
    /// The lanes of a warp are the lanes of one vectorized call, which execute
    /// in lockstep.
    /// </remarks>
    public override string EmitWarpBarrier() =>
        "/* warp barrier - vectorized execution, no synchronization needed */";

    /// <inheritdoc/>
    public override string EmitMemoryFence() =>
        "System.Threading.Thread.MemoryBarrier()";

    /// <inheritdoc/>
    public override string EmitPredicateBarrier(
        string predicate,
        PredicateBarrierKind kind,
        PredicateBarrierPredicateKind predicateKind) =>
        throw new NotSupportedException(
            "CPU backend uses CPUExpressionEmitter.EmitPredicateBarrier " +
            "directly; this path should not be reached.");

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
    public override string EmitBroadcast(
        string variable,
        string origin,
        BroadcastKind kind,
        ArithmeticBasicValueType type) =>
        throw new NotSupportedException(
            "CPU backend uses CPUExpressionEmitter.EmitBroadcast " +
            "directly; this path should not be reached.");

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
            ArithmeticBasicValueType.Float16 => "System.Half",
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
    /// Exclusive scans start at the identity of the operation; use the
    /// overload taking the identity of the scan instead.
    /// </remarks>
    public override string EmitWarpScan(
        string variable,
        BinaryArithmeticKind operation,
        WarpScanKind kind,
        ArithmeticBasicValueType type) =>
        EmitWarpScan(variable, operation, kind, type, identity: null);

    /// <summary>
    /// Emits a masked warp scan. Exclusive scans start at
    /// <paramref name="identity"/>, which is required for all operations but
    /// Add and Mul.
    /// </summary>
    /// <param name="variable">The per-lane values to scan.</param>
    /// <param name="operation">The scan operation.</param>
    /// <param name="kind">Inclusive or Exclusive.</param>
    /// <param name="type">The arithmetic value type.</param>
    /// <param name="identity">The identity of exclusive scans, if known.</param>
    public static string EmitWarpScan(
        string variable,
        BinaryArithmeticKind operation,
        WarpScanKind kind,
        ArithmeticBasicValueType type,
        string? identity)
    {
        var name = operation switch
        {
            BinaryArithmeticKind.Add => "Add",
            BinaryArithmeticKind.Mul => "Mul",
            BinaryArithmeticKind.Min => "Min",
            BinaryArithmeticKind.Max => "Max",
            BinaryArithmeticKind.And => "And",
            BinaryArithmeticKind.Or => "Or",
            BinaryArithmeticKind.Xor => "Xor",
            _ => throw new NotSupportedException(
                $"CPU warp scan does not support op {operation}")
        };
        var elemType = GetPrimitiveTypeName(type);
        var arguments = $"{variable}, {CPUExpressionEmitter.ActiveMaskName}";
        if (kind == WarpScanKind.Inclusive)
        {
            return $"CPUVectorIntrinsics.WarpScanInclusive{name}<{elemType}>(" +
                $"{arguments})";
        }
        if (identity is not null)
            arguments += $", ({elemType})({identity})";
        else if (operation is not BinaryArithmeticKind.Add
            and not BinaryArithmeticKind.Mul)
        {
            throw new NotSupportedException(
                $"CPU exclusive warp scan of op {operation} needs an identity");
        }
        return $"CPUVectorIntrinsics.WarpScanExclusive{name}<{elemType}>({arguments})";
    }

    #endregion

    #region Thread/Block Identification

    /// <summary>
    /// The per-lane indices of the lanes within their group. Every vectorized
    /// call runs up to vectorWidth threads of one group.
    /// </summary>
    internal const string GroupLanesName = "groupLanes";

    /// <summary>
    /// The per-lane indices of the lanes within their warp (0 to
    /// vectorWidth-1). The lanes of a call form one warp.
    /// </summary>
    internal const string WarpLanesName = "warpLanes";

    /// <summary>
    /// The thread-static field holding the index of the group being run.
    /// </summary>
    internal const string GridIndexName = "_gridIndex";

    /// <summary>
    /// The thread-static field holding the index of the first lane of a call
    /// within its group.
    /// </summary>
    internal const string GroupOffsetName = "_groupOffset";

    /// <summary>
    /// The thread-static field holding the number of groups of the launch.
    /// </summary>
    internal const string GridDimensionName = "_gridDimension";

    /// <summary>
    /// The thread-static field holding the number of threads per group.
    /// </summary>
    internal const string GroupDimensionName = "_groupDimension";

    /// <inheritdoc/>
    /// <remarks>
    /// The lanes of a call form one warp.
    /// </remarks>
    public override string EmitLaneIdx(int dimension) =>
        dimension == 0 ? WarpLanesName : "0";

    /// <inheritdoc/>
    /// <remarks>
    /// Groups larger than the vector width run one warp per call.
    /// </remarks>
    public override string EmitWarpIdx(int dimension) =>
        dimension == 0 ? $"({GroupOffsetName} / SIMDWidth)" : "0";

    /// <inheritdoc/>
    /// <remarks>
    /// Warp dimension is the vector width for dimension 0, 1 otherwise.
    /// </remarks>
    public override string EmitWarpDim(int dimension) =>
        dimension == 0 ? vectorWidth?.ToString() ?? "1" : "1";

    /// <inheritdoc/>
    /// <remarks>
    /// The index of a lane within its group.
    /// </remarks>
    public override string EmitThreadIdx(int dimension) =>
        dimension == 0 ? GroupLanesName : "0";

    /// <inheritdoc/>
    /// <remarks>
    /// The index of the group being run, set by the launcher.
    /// </remarks>
    public override string EmitBlockIdx(int dimension) =>
        dimension == 0 ? GridIndexName : "0";

    /// <inheritdoc/>
    /// <remarks>
    /// The group size of the launch, set by the launcher.
    /// </remarks>
    public override string EmitBlockDim(int dimension) =>
        dimension == 0 ? GroupDimensionName : "1";

    /// <inheritdoc/>
    /// <remarks>
    /// The number of groups of the launch, set by the launcher.
    /// </remarks>
    public override string EmitGridDim(int dimension) =>
        dimension == 0 ? GridDimensionName : "1";

    #endregion

    #region Constant Emission

    /// <inheritdoc/>
    public override string EmitHalfConstant(ushort rawBits) =>
        $"BitConverter.UInt16BitsToHalf(0x{rawBits:X4})";

    #endregion
}
