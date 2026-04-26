// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IntrinsicEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.Backends;

/// <summary>
/// Base class for emitting language-specific intrinsic functions.
/// Each backend provides its own implementation with target-specific syntax.
/// </summary>
/// <remarks>
/// All methods accept ArithmeticBasicValueType to generate type-specific intrinsics
/// with proper signedness handling. For example, Float32 generates sinf(), while
/// Float64 generates sin(). Unsigned integer types select unsigned-specific
/// intrinsics where applicable (e.g., umin vs min).
/// </remarks>
abstract class IntrinsicEmitter
{
    #region Mathematical Operations

    /// <summary>
    /// Emits a sine function call.
    /// </summary>
    /// <param name="arg">The argument expression.</param>
    /// <param name="type">The arithmetic type (Float32, Float64, etc.).</param>
    public abstract string EmitSin(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a cosine function call.
    /// </summary>
    public abstract string EmitCos(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a tangent function call.
    /// </summary>
    public abstract string EmitTan(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits an arc sine function call.
    /// </summary>
    public abstract string EmitAsin(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits an arc cosine function call.
    /// </summary>
    public abstract string EmitAcos(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits an arc tangent function call.
    /// </summary>
    public abstract string EmitAtan(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a two-argument arc tangent function call.
    /// </summary>
    public abstract string EmitAtan2(string y, string x, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a square root function call.
    /// </summary>
    public abstract string EmitSqrt(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a reciprocal square root function call (1/sqrt(x)).
    /// </summary>
    public abstract string EmitRsqrt(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits an exponential function call (e^x).
    /// </summary>
    public abstract string EmitExp(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a base-2 exponential function call (2^x).
    /// </summary>
    public abstract string EmitExp2(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a natural logarithm function call.
    /// </summary>
    public abstract string EmitLog(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a base-2 logarithm function call.
    /// </summary>
    public abstract string EmitLog2(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a base-10 logarithm function call.
    /// </summary>
    public abstract string EmitLog10(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a power function call (x^y).
    /// </summary>
    public abstract string EmitPow(string x, string y, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits an absolute value function call.
    /// </summary>
    public abstract string EmitAbs(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a minimum function call.
    /// </summary>
    public abstract string EmitMin(string arg1, string arg2, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a maximum function call.
    /// </summary>
    public abstract string EmitMax(string arg1, string arg2, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a floor function call (round down).
    /// </summary>
    public abstract string EmitFloor(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a ceiling function call (round up).
    /// </summary>
    public abstract string EmitCeil(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a truncate function call (round toward zero).
    /// </summary>
    public abstract string EmitTrunc(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a round function call (round to nearest).
    /// </summary>
    public abstract string EmitRound(string arg, ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a clamp function call (constrain value to range).
    /// </summary>
    public abstract string EmitClamp(
        string value,
        string min,
        string max,
        ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a fused multiply-add function call (a * b + c).
    /// </summary>
    /// <param name="a">First operand (multiplicand).</param>
    /// <param name="b">Second operand (multiplicand).</param>
    /// <param name="c">Third operand (addend).</param>
    /// <param name="type">The arithmetic type.</param>
    public abstract string EmitMultiplyAdd(
        string a,
        string b,
        string c,
        ArithmeticBasicValueType type);

    #endregion

    #region Atomic Operations

    /// <summary>
    /// Emits an atomic add operation.
    /// </summary>
    /// <param name="ptr">Pointer to the memory location.</param>
    /// <param name="value">Value to add.</param>
    /// <param name="type">The type of the operation (Int32, Int64, Float32, etc.).</param>
    /// <param name="addressSpace">
    /// Address space of the target pointer. Backends that need an
    /// address-space-qualified cast (e.g. Metal, where shared-memory
    /// atomics need <c>threadgroup</c> instead of <c>device</c>) use
    /// this; backends that infer the address space from the pointer
    /// expression (CUDA, ROCm, OpenCL, CPU) ignore it.
    /// </param>
    /// <returns>Code for atomic add operation.</returns>
    public abstract string EmitAtomicAdd(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace);

    /// <summary>
    /// Emits an atomic exchange operation (swap).
    /// </summary>
    public abstract string EmitAtomicExchange(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace);

    /// <summary>
    /// Emits an atomic compare-and-swap operation.
    /// </summary>
    /// <param name="ptr">Pointer to the memory location.</param>
    /// <param name="compare">Expected value.</param>
    /// <param name="value">New value if comparison succeeds.</param>
    /// <param name="type">The type of the operation.</param>
    /// <param name="addressSpace">Address space of the target pointer.</param>
    public abstract string EmitAtomicCAS(
        string ptr,
        string compare,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace);

    /// <summary>
    /// Emits an atomic minimum operation.
    /// </summary>
    public abstract string EmitAtomicMin(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace);

    /// <summary>
    /// Emits an atomic maximum operation.
    /// </summary>
    public abstract string EmitAtomicMax(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace);

    /// <summary>
    /// Emits an atomic bitwise AND operation.
    /// </summary>
    public abstract string EmitAtomicAnd(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace);

    /// <summary>
    /// Emits an atomic bitwise OR operation.
    /// </summary>
    public abstract string EmitAtomicOr(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace);

    /// <summary>
    /// Emits an atomic bitwise XOR operation.
    /// </summary>
    public abstract string EmitAtomicXor(
        string ptr,
        string value,
        ArithmeticBasicValueType type,
        MemoryAddressSpace addressSpace);

    #endregion

    #region Bit-Reinterpret Casts

    /// <summary>
    /// Emits a bit-reinterpret cast from a float-typed expression to an
    /// integer-typed expression of the same width (e.g. CUDA
    /// <c>__double_as_longlong</c>, OpenCL <c>as_long</c>).
    /// </summary>
    /// <param name="arg">The source expression.</param>
    /// <param name="sourceType">The source basic value type (Float16/32/64).</param>
    /// <param name="targetType">The target basic value type (Int16/32/64).</param>
    public abstract string EmitFloatAsInt(
        string arg,
        BasicValueType sourceType,
        BasicValueType targetType);

    /// <summary>
    /// Emits a bit-reinterpret cast from an integer-typed expression to a
    /// float-typed expression of the same width (e.g. CUDA
    /// <c>__longlong_as_double</c>, OpenCL <c>as_double</c>).
    /// </summary>
    /// <param name="arg">The source expression.</param>
    /// <param name="sourceType">The source basic value type (Int16/32/64).</param>
    /// <param name="targetType">The target basic value type (Float16/32/64).</param>
    public abstract string EmitIntAsFloat(
        string arg,
        BasicValueType sourceType,
        BasicValueType targetType);

    #endregion

    #region Synchronization

    /// <summary>
    /// Emits a thread group/workgroup barrier.
    /// </summary>
    public abstract string EmitBarrier();

    /// <summary>
    /// Emits a memory fence to ensure memory consistency.
    /// </summary>
    public abstract string EmitMemoryFence();

    #endregion

    #region Warp/Wave Primitives

    /// <summary>
    /// Emits a shuffle operation.
    /// </summary>
    /// <param name="variable">The variable to shuffle.</param>
    /// <param name="origin">The source lane/offset (meaning depends on kind).</param>
    /// <param name="kind">The shuffle kind.</param>
    /// <param name="type">The value type being shuffled.</param>
    public abstract string EmitShuffle(
        string variable,
        string origin,
        ShuffleKind kind,
        ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a broadcast operation.
    /// </summary>
    /// <param name="variable">The variable to broadcast.</param>
    /// <param name="origin">The source thread index.</param>
    /// <param name="kind">The broadcast kind (warp or group level).</param>
    /// <param name="type">The value type being broadcast.</param>
    public abstract string EmitBroadcast(
        string variable,
        string origin,
        BroadcastKind kind,
        ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a native warp reduce intrinsic. Only called for backends that
    /// advertise native support; others are lowered to shuffles before codegen.
    /// </summary>
    /// <param name="variable">The input value expression.</param>
    /// <param name="operation">The recognized binary arithmetic operation.</param>
    /// <param name="kind">Reduce (first-lane) or AllReduce (all-lanes).</param>
    /// <param name="type">The arithmetic value type.</param>
    public abstract string EmitWarpReduce(
        string variable,
        BinaryArithmeticKind operation,
        WarpReduceKind kind,
        ArithmeticBasicValueType type);

    /// <summary>
    /// Emits a native warp scan intrinsic. Only called for backends that
    /// advertise native support; others are lowered to shuffles before codegen.
    /// </summary>
    /// <param name="variable">The input value expression.</param>
    /// <param name="operation">The recognized binary arithmetic operation.</param>
    /// <param name="kind">Inclusive or Exclusive.</param>
    /// <param name="type">The arithmetic value type.</param>
    public abstract string EmitWarpScan(
        string variable,
        BinaryArithmeticKind operation,
        WarpScanKind kind,
        ArithmeticBasicValueType type);

    #endregion

    #region Thread/Block Identification

    /// <summary>
    /// Emits lane index within a warp (relative thread index within a warp).
    /// </summary>
    /// <param name="dimension">0=x, 1=y, 2=z</param>
    public abstract string EmitLaneIdx(int dimension);

    /// <summary>
    /// Emits warp index within a block/workgroup.
    /// </summary>
    /// <param name="dimension">0=x, 1=y, 2=z</param>
    public abstract string EmitWarpIdx(int dimension);

    /// <summary>
    /// Emits warp dimension (size of a warp).
    /// </summary>
    /// <param name="dimension">0=x, 1=y, 2=z</param>
    public abstract string EmitWarpDim(int dimension);

    /// <summary>
    /// Emits thread index within a block/workgroup.
    /// </summary>
    /// <param name="dimension">0=x, 1=y, 2=z</param>
    public abstract string EmitThreadIdx(int dimension);

    /// <summary>
    /// Emits block/workgroup index within the grid.
    /// </summary>
    /// <param name="dimension">0=x, 1=y, 2=z</param>
    public abstract string EmitBlockIdx(int dimension);

    /// <summary>
    /// Emits block/workgroup dimension (size).
    /// </summary>
    /// <param name="dimension">0=x, 1=y, 2=z</param>
    public abstract string EmitBlockDim(int dimension);

    /// <summary>
    /// Emits grid dimension (number of blocks/workgroups).
    /// </summary>
    /// <param name="dimension">0=x, 1=y, 2=z</param>
    public abstract string EmitGridDim(int dimension);

    #endregion

    #region Debug and Utility

    /// <summary>
    /// Emits a printf statement for device-side output.
    /// </summary>
    /// <param name="format">Format string.</param>
    /// <param name="args">Arguments to format.</param>
    /// <returns>Printf statement code, or throws if not supported.</returns>
    public abstract string EmitPrintf(string format, params string[] args);

    /// <summary>
    /// Emits an assertion check.
    /// </summary>
    /// <param name="condition">Condition expression to assert.</param>
    /// <param name="message">
    /// Optional message (may not be supported on all platforms).
    /// </param>
    /// <returns>Assert statement code, or custom implementation.</returns>
    public abstract string EmitAssert(string condition, string? message = null);

    #endregion

    #region Utility Methods

    /// <summary>
    /// Returns true if printf is supported by this backend.
    /// </summary>
    public abstract bool SupportsPrintf { get; }

    /// <summary>
    /// Returns true if native assertions are supported by this backend.
    /// </summary>
    public abstract bool SupportsAssert { get; }

    #endregion

    #region Constant Emission

    /// <summary>
    /// Emits a Half (Float16) constant as a bit-precise value.
    /// </summary>
    /// <param name="rawBits">The raw 16-bit representation of the half value.</param>
    /// <returns>Backend-specific code for the half constant.</returns>
    /// <remarks>
    /// This emits the constant as a bit-precise representation to avoid precision
    /// loss during parsing. For example, in CUDA this might emit:
    /// __float2half_rn(*(const __half*)&amp;(ushort){0x3C00})
    /// or as_half((ushort)0x3C00) depending on backend support.
    /// </remarks>
    public abstract string EmitHalfConstant(ushort rawBits);

    #endregion
}
