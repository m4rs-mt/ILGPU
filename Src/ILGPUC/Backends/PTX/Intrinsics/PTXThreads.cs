// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXThreads.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.ScanReduce;
using ILGPUC.Intrinsic;
using ILGPUC.IR.Values;

namespace ILGPUC.Backends.PTX.Intrinsics;

static partial class PTXThreads
{
    /// <summary>
    /// A warp member mask that considers all threads in a warp.
    /// </summary>
    public const uint AllLanesMask = 0xffffffff;

    /// <summary>
    /// The basic mask that has be combined with an 'or' command
    /// in case of a <see cref="ShuffleKind.Xor"/> or a <see cref="ShuffleKind.Down"/>
    /// shuffle instruction.
    /// </summary>
    public const int XorDownMask = 0x1f;

    /// <summary>
    /// Returns the accelerator type <see cref="AcceleratorType.Cuda"/>;
    /// </summary>
    public static AcceleratorType AcceleratorType() => ILGPU.Runtime.AcceleratorType.Cuda;

    /// <summary>
    /// Returns the current warp size.
    /// </summary>
    public static int WarpSize() => PTXBackend.WarpSize;

    #region Barriers

    /// <summary>
    /// Wraps a single warp-barrier operation.
    /// </summary>
    public static void BarrierWarpLevel() =>
        CudaAsm.Emit($"bar.warp.sync {AllLanesMask}");

    /// <summary>
    /// Wraps a single group-barrier operation.
    /// </summary>
    public static void BarrierGroupLevel() =>
        CudaAsm.Emit("bar.sync 0");

    #endregion

    #region Predicate Barriers

    /// <summary>
    /// Wraps a single predicate warp-barrier operation.
    /// </summary>
    public static bool PredicateBarrierWarpLevelAnd(bool predicate)
    {
        int result = GenericWarp.AllReduce<int, AndInt32>(predicate ? 1 : 0);
        BarrierWarpLevel();
        return result != 0;
    }

    /// <summary>
    /// Wraps a single predicate warp-barrier operation.
    /// </summary>
    public static bool PredicateBarrierWarpLevelOr(bool predicate)
    {
        int result = GenericWarp.AllReduce<int, OrInt32>(predicate ? 1 : 0);
        BarrierWarpLevel();
        return result != 0;
    }

    /// <summary>
    /// Wraps a single predicate warp-barrier operation.
    /// </summary>
    public static int PredicateBarrierWarpLevelPopCount(bool predicate)
    {
        int result = GenericWarp.AllReduce<int, AddInt32>(predicate ? 1 : 0);
        BarrierWarpLevel();
        return result;
    }

    /// <summary>
    /// Wraps a single predicate group-barrier operation.
    /// </summary>
    public static bool PredicateBarrierGroupLevelAnd(bool predicate)
    {
        CudaAsm.Emit($"bar.red.and.pred {{0}}, 0, {{1}}", out bool result, predicate);
        return result;
    }

    /// <summary>
    /// Wraps a single predicate group-barrier operation.
    /// </summary>
    public static bool PredicateBarrierGroupLevelOr(bool predicate)
    {
        CudaAsm.Emit($"bar.red.or.pred {{0}}, 0, {{1}}", out bool result, predicate);
        return result;
    }

    /// <summary>
    /// Wraps a single predicate group-barrier operation.
    /// </summary>
    public static int PredicateBarrierGroupLevelPopCount(bool predicate)
    {
        CudaAsm.Emit($"bar.red.popc.pred {{0}}, 0, {{1}}", out int result, predicate);
        return result;
    }

    #endregion

    #region Memory Barriers

    /// <summary>
    /// Wraps a single memory group-barrier operation.
    /// </summary>
    public static void MemoryBarrierGroupLevel() =>
        CudaAsm.Emit("membar.cta");

    /// <summary>
    /// Wraps a single memory device-barrier operation.
    /// </summary>
    public static void MemoryBarrierDeviceLevel() =>
        CudaAsm.Emit("membar.gl");

    /// <summary>
    /// Wraps a single memory system-barrier operation.
    /// </summary>
    public static void MemoryBarrierSystemLevel() =>
        CudaAsm.Emit("membar.sys");

    #endregion

    #region Shuffles

    /// <summary>
    /// Wraps a single warp-shuffle operation.
    /// </summary>
    public static uint WarpShuffleInt32(uint value, int idx)
    {
        CudaAsm.Emit(
            $"shfl.sync.idx.b32 {{0}}, {{1}}, {{2}}, {XorDownMask}, {AllLanesMask}",
            out uint result,
            value,
            idx);
        return result;
    }

    /// <summary>
    /// Wraps a single warp-shuffle operation.
    /// </summary>
    public static uint WarpShuffleDownInt32(uint value, int idx)
    {
        CudaAsm.Emit(
            $"shfl.sync.down.b32 {{0}}, {{1}}, {{2}}, {XorDownMask}, {AllLanesMask}",
            out uint result,
            value,
            idx);
        return result;
    }

    /// <summary>
    /// Wraps a single warp-shuffle operation.
    /// </summary>
    public static uint WarpShuffleUpInt32(uint value, int idx)
    {
        CudaAsm.Emit(
            $"shfl.sync.down.b32 {{0}}, {{1}}, {{2}}, 0, {AllLanesMask}",
            out uint result,
            value,
            idx);
        return result;
    }

    /// <summary>
    /// Wraps a single warp-shuffle operation.
    /// </summary>
    public static uint WarpShuffleXorInt32(uint value, int idx)
    {
        CudaAsm.Emit(
            $"shfl.sync.bfly.b32 {{0}}, {{1}}, {{2}}, {XorDownMask}, {AllLanesMask}",
            out uint result,
            value,
            idx);
        return result;
    }

    #endregion
}
