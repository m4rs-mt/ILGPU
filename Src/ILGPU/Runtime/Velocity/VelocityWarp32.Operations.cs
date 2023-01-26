// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityWarp32.Operations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using static ILGPU.Runtime.Velocity.VelocityWarpOperations32;
using Arm64Intrinsics = System.Runtime.Intrinsics.Arm.AdvSimd.Arm64;

namespace ILGPU.Runtime.Velocity
{
    partial struct VelocityWarp32
    {
        #region Unary Operations

        public VelocityWarp32 NegI() => -As<int>();

        public VelocityWarp32 NegU() => ~As<uint>();

        public VelocityWarp32 NegF() => ~As<float>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 Not()
        {
            // Special implementation for ARM
            if (IsVector128 && AdvSimd.IsSupported)
                return AdvSimd.Not(As<uint>().AsVector128()).AsVector();

            return Vector.OnesComplement(As<uint>());
        }

        public VelocityWarp32 AbsU() => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 AbsI() => Vector.Abs(As<int>());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 AbsF() => Vector.Abs(As<float>());

        private readonly struct PopCScalarOperation : IScalarIOperation
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Apply(int index, int value) => IntrinsicMath.PopCount(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 PopC()
        {
            // Special implementation for ARM
            if (IsVector128 && Arm64Intrinsics.IsSupported)
            {
                // Determine the pop count per lane
                var popCountPerByte = AdvSimd.PopCount(As<byte>().AsVector128());
                var popCountPerLane =
                    Arm64Intrinsics.AddPairwise(
                        Arm64Intrinsics.AddPairwise(
                            popCountPerByte,
                            Vector128<byte>.Zero),
                        Vector128<byte>.Zero);
                // Distribute the pop-count values to all lanes
                var lower = AdvSimd.VectorTableLookup(
                    popCountPerLane,
                    First2BytesToIntAdvSimd.AsByte());
                var upper = AdvSimd.VectorTableLookup(
                    popCountPerLane,
                    Second2BytesToIntAdvSimd.AsByte());
                return Vector128.Create(lower, upper).AsInt32().AsVector();
            }

            return this.ApplyScalarIOperation(new PopCScalarOperation());
        }

        private readonly struct RcpFScalarOperation : IScalarFOperation
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float Apply(int index, float value) => 1.0f / value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 RcpF()
        {
            if (IsVector128)
            {
                // Special implementation for ARM
                if (AdvSimd.IsSupported)
                {
                    return AdvSimd.ReciprocalEstimate(As<float>().AsVector128())
                        .AsVector();
                }

                // Special implementation for X86
                if (Sse.IsSupported)
                {
                    return Sse.Reciprocal(As<float>().AsVector128())
                        .AsVector();
                }
            }

            // Special implementation for X86
            if (IsVector256 && Avx.IsSupported)
            {
                return Avx.Reciprocal(As<float>().AsVector256())
                    .AsVector();
            }

            return this.ApplyScalarFOperation(new RcpFScalarOperation());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 IsNotNanF() =>
            Vector.Equals(As<float>(), As<float>());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 IsNanF() => IsNotNanF().Not();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 SqrtF() => Vector.SquareRoot(As<float>());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 RsqrtF()
        {
            // Special implementation for ARM
            if (IsVector128 && AdvSimd.IsSupported)
            {
                return AdvSimd.ReciprocalSquareRootEstimate(
                        As<float>().AsVector128())
                    .AsVector();
            }
            return SqrtF().RcpF();
        }

        #endregion

        #region Binary Operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 And(VelocityWarp32 other) =>
            Vector.BitwiseAnd(warpData, other.warpData);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 Or(VelocityWarp32 other) =>
            Vector.BitwiseOr(warpData, other.warpData);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 AndNot(VelocityWarp32 other) =>
            Vector.AndNot(warpData, other.warpData);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 Xor(VelocityWarp32 other) =>
            Or(other).AndNot(And(other));

        #endregion

        #region Ternary Operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 MultiplyAddI(VelocityWarp32 second, VelocityWarp32 third)
        {
            // Special implementation for ARM
            if (IsVector128 && AdvSimd.IsSupported)
            {
                var sourceVec = As<int>().AsVector128();
                var secondVec = second.As<int>().AsVector128();
                var thirdVec = second.As<int>().AsVector128();

                return AdvSimd.MultiplyAdd(thirdVec, sourceVec, secondVec).AsVector();
            }

            return this.MulI(second).AddI(third);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 MultiplyAddU(VelocityWarp32 second, VelocityWarp32 third)
        {
            // Special implementation for ARM
            if (IsVector128 && AdvSimd.IsSupported)
            {
                var sourceVec = As<uint>().AsVector128();
                var secondVec = second.As<uint>().AsVector128();
                var thirdVec = second.As<uint>().AsVector128();

                return AdvSimd.MultiplyAdd(thirdVec, sourceVec, secondVec).AsVector();
            }

            return this.MulU(second).AddU(third);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 MultiplyAddF(VelocityWarp32 second, VelocityWarp32 third)
        {
            if (IsVector128)
            {
                var sourceVec = As<float>().AsVector128();
                var secondVec = second.As<float>().AsVector128();
                var thirdVec = second.As<float>().AsVector128();

                // Special implementation for ARM
                if (AdvSimd.IsSupported)
                    AdvSimd.FusedMultiplyAdd(thirdVec, sourceVec, secondVec);

                // Special implementation for X86
                if (Fma.IsSupported)
                    return Fma.MultiplyAdd(sourceVec, secondVec, thirdVec).AsVector();
            }

            // Special implementation for X86
            if (IsVector256 && Fma.IsSupported)
            {
                var sourceVec = As<float>().AsVector256();
                var secondVec = second.As<float>().AsVector256();
                var thirdVec = second.As<float>().AsVector256();
                return Fma.MultiplyAdd(sourceVec, secondVec, thirdVec).AsVector();
            }

            return this.Mul(second).Add(third);
        }

        #endregion
    }

    partial class VelocityWarpOperations32
    {
        #region General Operations

        /// <summary>
        /// Dumps the given warp to the default console output.
        /// </summary>
        public static void Dump(this VelocityWarp32 warp) =>
            Console.WriteLine(warp.ToString());

        /// <summary>
        /// Converts the given half into its raw format.
        /// </summary>
        public static float FromHalf(Half half) => half;

        /// <summary>
        /// Implements a lane index vector for a 32bit warp.
        /// </summary>
        public static VelocityWarp32 GetLaneIndexVector() =>
            VelocityWarp32.LaneIndexVector;

        #endregion

        #region Group Operations

        /// <summary>
        /// Implements a barrier pop-count operation for a 32bit warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 GroupBarrierPopCount(
            VelocityWarp32 warp,
            VelocityLaneMask mask) =>
            warp.BarrierPopCount(mask);

        /// <summary>
        /// Implements a logical and barrier and operation for a 32bit warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 GroupBarrierAnd(
            VelocityWarp32 warp,
            VelocityLaneMask mask) =>
            warp.BarrierAnd(mask);

        /// <summary>
        /// Implements a logical barrier or operation for a 32bit warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 GroupBarrierOr(
            VelocityWarp32 warp,
            VelocityLaneMask mask) =>
            warp.BarrierOr(mask);

        /// <summary>
        /// Implements a logical group broadcast for a 32bit warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 GroupBroadcast<TVerifier>(
            VelocityWarp32 warp,
            VelocityWarp32 sourceLanes)
            where TVerifier : struct, IVelocityWarpVerifier =>
            warp.Broadcast<TVerifier>(sourceLanes);

        #endregion

        #region Warp Operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 WarpBroadcast<TVerifier>(
            VelocityWarp32 warp,
            VelocityWarp32 sourceLanes)
            where TVerifier : struct, IVelocityWarpVerifier =>
            GroupBroadcast<TVerifier>(warp, sourceLanes);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 WarpShuffle<TVerifier>(
            VelocityWarp32 warp,
            VelocityWarp32 sourceLanes)
            where TVerifier : struct, IVelocityWarpVerifier =>
            warp.Shuffle<TVerifier>(sourceLanes);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 WarpShuffleDown<TVerifier>(
            VelocityWarp32 warp,
            VelocityWarp32 delta)
            where TVerifier : struct, IVelocityWarpVerifier =>
            warp.ShuffleDown<TVerifier>(delta, VelocityWarp32.LengthVector);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 WarpShuffleUp<TVerifier>(
            VelocityWarp32 warp,
            VelocityWarp32 delta)
            where TVerifier : struct, IVelocityWarpVerifier =>
            warp.ShuffleUp<TVerifier>(delta, VelocityWarp32.LengthVector);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 WarpShuffleXor<TVerifier>(
            VelocityWarp32 warp,
            VelocityWarp32 delta)
            where TVerifier : struct, IVelocityWarpVerifier =>
            warp.ShuffleXor<TVerifier>(delta, VelocityWarp32.LengthVector);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 SubWarpShuffleDown<TVerifier>(
            VelocityWarp32 warp,
            VelocityWarp32 delta,
            VelocityWarp32 width)
            where TVerifier : struct, IVelocityWarpVerifier =>
            warp.ShuffleDown<TVerifier>(delta, width);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 SubWarpShuffleUp<TVerifier>(
            VelocityWarp32 warp,
            VelocityWarp32 delta,
            VelocityWarp32 width)
            where TVerifier : struct, IVelocityWarpVerifier =>
            warp.ShuffleUp<TVerifier>(delta, width);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 SubWarpShuffleXor<TVerifier>(
            VelocityWarp32 warp,
            VelocityWarp32 delta,
            VelocityWarp32 width)
            where TVerifier : struct, IVelocityWarpVerifier =>
            warp.ShuffleXor<TVerifier>(delta, width);

        #endregion

        #region Merge Operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 Merge(
            this VelocityWarp32 left,
            VelocityWarp32 right,
            VelocityWarp32 rightMask) =>
            Vector.ConditionalSelect(
                rightMask.As<int>(),
                right.As<int>(),
                left.As<int>());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 MergeWithMask(
            this VelocityWarp32 left,
            VelocityWarp32 right,
            VelocityLaneMask rightMask)
        {
            var maskVector = VelocityWarp32.FromMask(rightMask);
            return Merge(left, right, maskVector);
        }

        #endregion

        #region Convert Operations

        /// <summary>
        /// Does not perform a conversion.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 ConvertNop(this VelocityWarp32 value) => value;

        /// <summary>
        /// Converts the given 32bit integer warp to a 32bit integer warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 ConvertIToI(this VelocityWarp32 value) =>
            ConvertNop(value);

        /// <summary>
        /// Converts the given 32bit unsigned integer warp to a 32bit unsigned integer
        /// warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 ConvertUToU(this VelocityWarp32 value) =>
            ConvertNop(value);

        /// <summary>
        /// Converts the given 32bit float warp to a 32bit float warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 ConvertFToF(this VelocityWarp32 value) =>
            ConvertNop(value);

        /// <summary>
        /// Converts the given 32bit integer warp to a 32bit unsigned integer warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 ConvertIToU(this VelocityWarp32 value) =>
            ConvertNop(value);

        /// <summary>
        /// Converts the given 32bit unsigned integer warp to a 32bit integer warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 ConvertUToI(this VelocityWarp32 value) =>
            ConvertNop(value);

        /// <summary>
        /// Converts the given 32bit integer warp to a 32bit float warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 ConvertIToF(this VelocityWarp32 value)
        {
            var rawVector = Vector.ConvertToSingle(value.As<int>());
            return new VelocityWarp32(rawVector);
        }

        /// <summary>
        /// Converts the given 32bit unsigned integer warp to a 32bit float warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 ConvertUToF(this VelocityWarp32 value)
        {
            var rawVector = Vector.ConvertToSingle(value.As<uint>());
            return new VelocityWarp32(rawVector);
        }

        /// <summary>
        /// Converts the given 32bit float warp to a 32bit integer warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 ConvertFToI(this VelocityWarp32 value)
        {
            var rawVector = Vector.ConvertToInt32(value.As<float>());
            return new VelocityWarp32(rawVector);
        }

        /// <summary>
        /// Converts the given 32bit float warp to a 32bit unsigned integer warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 ConvertFToU(this VelocityWarp32 value)
        {
            var rawVector = Vector.ConvertToUInt32(value.As<float>());
            return new VelocityWarp32(rawVector);
        }

        /// <summary>
        /// Widens the given warp to a 64bit long warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 WidenI(this VelocityWarp32 warp)
        {
            Vector.Widen(warp.As<int>(), out var low, out var high);
            return new VelocityWarp64(low, high);
        }

        /// <summary>
        /// Widens the given warp to a 64bit ulong warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 WidenU(this VelocityWarp32 warp)
        {
            Vector.Widen(warp.As<uint>(), out var low, out var high);
            return new VelocityWarp64(low, high);
        }

        /// <summary>
        /// Widens the given warp to a 64bit double warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 WidenF(this VelocityWarp32 warp)
        {
            Vector.Widen(warp.As<float>(), out var low, out var high);
            return new VelocityWarp64(low, high);
        }

        #endregion

        #region Binary Operations

        public static VelocityWarp32 ComputeRemI(
            this VelocityWarp32 left,
            VelocityWarp32 right) =>
            SubI(left, MulU(DivI(left, right), right));

        public static VelocityWarp32 ComputeRemU(
            this VelocityWarp32 left,
            VelocityWarp32 right) =>
            SubU(left, MulU(DivU(left, right), right));

        public static VelocityWarp32 ComputeRemF(
            this VelocityWarp32 left,
            VelocityWarp32 right) =>
            Sub(left, Abs(Mul(Div(left, right), right)));

        #endregion
    }

    partial class VelocityOperations
    {
        #region General 32bit Operations

        private readonly MethodInfo[] convertWidenOperations32 = new MethodInfo[3]
        {
            GetMethod(
                typeof(VelocityWarpOperations32),
                nameof(VelocityWarpOperations32.WidenI)),
            GetMethod(
                typeof(VelocityWarpOperations32),
                nameof(VelocityWarpOperations32.WidenU)),
            GetMethod(
                typeof(VelocityWarpOperations32),
                nameof(VelocityWarpOperations32.WidenF)),
        };
        private readonly MethodInfo[] broadcastOperations32 = new MethodInfo[2];

        /// <summary>
        /// Initializes special 32bit warp operations.
        /// </summary>
        /// <param name="operationType32">The metadata operation type.</param>
        private void InitVelocityOperations32(Type operationType32)
        {
            broadcastOperations32[0] = GetMethod(
                operationType32,
                nameof(GroupBroadcast));
            broadcastOperations32[1] = GetMethod(
                operationType32,
                nameof(WarpBroadcast));

            DumpMethod32 = GetMethod(
                operationType32,
                nameof(VelocityWarpOperations32.Dump));
            FromHalfMethod = GetMethod(
                operationType32,
                nameof(FromHalf));
            LaneIndexVectorOperation32 = GetMethod(
                operationType32,
                nameof(GetLaneIndexVector));

            MergeOperation32 = GetMethod(
                operationType32,
                nameof(VelocityWarpOperations32.Merge));
            MergeWithMaskOperation32 = GetMethod(
                operationType32,
                nameof(VelocityWarpOperations32.MergeWithMask));

            FromMaskOperation32 = GetMethod(
                typeof(VelocityWarp32),
                nameof(VelocityWarp32.FromMask));
            ToMaskOperation32 = GetMethod(
                typeof(VelocityWarp32),
                nameof(VelocityWarp32.ToMask));

            InitWarpOperations32(operationType32);
            InitGroupOperations32(operationType32);
        }

        public MethodInfo DumpMethod32 { get; private set; }
        public MethodInfo FromHalfMethod { get; private set; }

        public MethodInfo LaneIndexVectorOperation32 { get; private set; }

        public MethodInfo MergeOperation32 { get; private set; }
        public MethodInfo MergeWithMaskOperation32 { get; private set; }

        public MethodInfo FromMaskOperation32 { get; private set; }
        public MethodInfo ToMaskOperation32 { get; private set; }

        public MethodInfo GetConvertWidenOperation32(VelocityWarpOperationMode mode) =>
            convertWidenOperations32[(int)mode];

        #endregion

        #region Constant Values

        private readonly MethodInfo[] constValueOperations32 = new MethodInfo[]
        {
            GetMethod(typeof(VelocityWarp32), nameof(VelocityWarp32.GetConstI)),
            GetMethod(typeof(VelocityWarp32), nameof(VelocityWarp32.GetConstU)),
            GetMethod(typeof(VelocityWarp32), nameof(VelocityWarp32.GetConstF)),
        };

        public MethodInfo GetConstValueOperation32(VelocityWarpOperationMode mode) =>
            constValueOperations32[(int)mode];

        #endregion

        #region Warp Operations

        private readonly Dictionary<ShuffleKind, MethodInfo> warpOperations32 =
            new Dictionary<ShuffleKind, MethodInfo>();
        private readonly Dictionary<ShuffleKind, MethodInfo> subWarpOperations32 =
            new Dictionary<ShuffleKind, MethodInfo>();

        private void InitWarpOperations32(Type operationType32)
        {
            warpOperations32.Add(ShuffleKind.Generic,
                GetMethod(operationType32, nameof(WarpShuffle)));
            warpOperations32.Add(ShuffleKind.Down,
                GetMethod(operationType32, nameof(WarpShuffleDown)));
            warpOperations32.Add(ShuffleKind.Up,
                GetMethod(operationType32, nameof(WarpShuffleUp)));
            warpOperations32.Add(ShuffleKind.Xor,
                GetMethod(operationType32, nameof(WarpShuffleXor)));

            subWarpOperations32.Add(ShuffleKind.Down,
                GetMethod(operationType32, nameof(SubWarpShuffleDown)));
            subWarpOperations32.Add(ShuffleKind.Up,
                GetMethod(operationType32, nameof(SubWarpShuffleUp)));
            subWarpOperations32.Add(ShuffleKind.Xor,
                GetMethod(operationType32, nameof(SubWarpShuffleXor)));
        }

        public MethodInfo GetWarpShuffleOperation32(
            ShuffleKind kind,
            Type warpVerifier) =>
            warpOperations32[kind].MakeGenericMethod(warpVerifier);
        public MethodInfo GetSubWarpShuffleOperation32(
            ShuffleKind kind,
            Type warpVerifier) =>
            subWarpOperations32[kind].MakeGenericMethod(warpVerifier);

        public MethodInfo GetWarpBroadcastOperation32(Type warpVerifier) =>
            broadcastOperations32[1].MakeGenericMethod(warpVerifier);

        #endregion

        #region Group Operations

        private readonly Dictionary<
            PredicateBarrierKind,
            MethodInfo> groupPredicateBarrierOperations32 =
            new Dictionary<PredicateBarrierKind, MethodInfo>();

        private void InitGroupOperations32(Type operationType32)
        {
            groupPredicateBarrierOperations32.Add(PredicateBarrierKind.PopCount,
                GetMethod(operationType32, nameof(GroupBarrierPopCount)));
            groupPredicateBarrierOperations32.Add(PredicateBarrierKind.And,
                GetMethod(operationType32, nameof(GroupBarrierAnd)));
            groupPredicateBarrierOperations32.Add(PredicateBarrierKind.Or,
                GetMethod(operationType32, nameof(GroupBarrierOr)));
        }

        public MethodInfo GetGroupBroadcastOperation32(Type warpVerifier) =>
            broadcastOperations32[0].MakeGenericMethod(warpVerifier);

        public MethodInfo GetGroupPredicateBarrierOperation32(
            PredicateBarrierKind kind) =>
            groupPredicateBarrierOperations32[kind];

        #endregion
    }
}
