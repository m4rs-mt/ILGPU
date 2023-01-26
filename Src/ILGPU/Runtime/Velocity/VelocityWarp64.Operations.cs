// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityWarp64.Operations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using static ILGPU.Runtime.Velocity.VelocityWarpOperations64;

namespace ILGPU.Runtime.Velocity
{
    partial struct VelocityWarp64
    {
        #region Unary Operations

        public VelocityWarp64 NegI() =>
            new VelocityWarp64(-LowerAs<long>(), -UpperAs<long>());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 NegU() =>
            new VelocityWarp64(~LowerAs<ulong>(), ~UpperAs<ulong>());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 NegF() =>
            new VelocityWarp64(~LowerAs<double>(), ~UpperAs<double>());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 Not()
        {
            // Special implementation for ARM
            if (IsVector128 && AdvSimd.IsSupported)
            {
                return new VelocityWarp64(
                    AdvSimd.Not(LowerAs<ulong>().AsVector128()).AsVector(),
                    AdvSimd.Not(UpperAs<ulong>().AsVector128()).AsVector());
            }

            return new VelocityWarp64(
                Vector.OnesComplement(LowerAs<ulong>()),
                Vector.OnesComplement(UpperAs<ulong>()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 AbsI() =>
            new VelocityWarp64(
                Vector.Abs(LowerAs<long>()),
                Vector.Abs(UpperAs<long>()));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 AbsF() =>
            new VelocityWarp64(
                Vector.Abs(LowerAs<double>()),
                Vector.Abs(UpperAs<double>()));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 IsNotNanF() =>
            new VelocityWarp64(
                Vector.Equals(LowerAs<double>(), LowerAs<double>()),
                Vector.Equals(UpperAs<double>(), UpperAs<double>()));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 IsNanF() => IsNotNanF().Not();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 IsNanF32() => IsNanF().NarrowI();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 SqrtF() =>
            new VelocityWarp64(
                Vector.SquareRoot(LowerAs<double>()),
                Vector.SquareRoot(UpperAs<double>()));

        #endregion

        #region Binary Operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 And(VelocityWarp64 other) =>
            new VelocityWarp64(
                Vector.BitwiseAnd(lowerData, other.lowerData),
                Vector.BitwiseAnd(upperData, other.upperData));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 Or(VelocityWarp64 other) =>
            new VelocityWarp64(
                Vector.BitwiseOr(lowerData, other.lowerData),
                Vector.BitwiseOr(upperData, other.upperData));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 AndNot(VelocityWarp64 other) =>
            new VelocityWarp64(
                Vector.AndNot(lowerData, other.lowerData),
                Vector.AndNot(upperData, other.upperData));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 Xor(VelocityWarp64 other) =>
            Or(other).AndNot(And(other));

        #endregion

        #region Ternary Operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 MultiplyAddI(VelocityWarp64 second, VelocityWarp64 third) =>
            this.MulI(second).AddI(third);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 MultiplyAddU(VelocityWarp64 second, VelocityWarp64 third) =>
            this.MulU(second).AddU(third);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 MultiplyAddF(VelocityWarp64 second, VelocityWarp64 third)
        {
            // Special implementation for X86
            if (IsVector128 && Fma.IsSupported)
            {
                return new VelocityWarp64(
                    Fma.MultiplyAdd(
                        LowerAs<double>().AsVector128(),
                        second.LowerAs<double>().AsVector128(),
                        second.LowerAs<double>().AsVector128()).AsVector(),
                    Fma.MultiplyAdd(
                        UpperAs<double>().AsVector128(),
                        second.UpperAs<double>().AsVector128(),
                        second.UpperAs<double>().AsVector128()).AsVector());
            }

            // Special implementation for X86
            if (IsVector256 && Fma.IsSupported)
            {
                return new VelocityWarp64(
                    Fma.MultiplyAdd(
                        LowerAs<double>().AsVector256(),
                        second.LowerAs<double>().AsVector256(),
                        second.LowerAs<double>().AsVector256()).AsVector(),
                    Fma.MultiplyAdd(
                        UpperAs<double>().AsVector256(),
                        second.UpperAs<double>().AsVector256(),
                        second.UpperAs<double>().AsVector256()).AsVector());
            }

            return this.Mul(second).Add(third);
        }

        #endregion
    }

    partial class VelocityWarpOperations64
    {
        #region General Operations

        /// <summary>
        /// Dumps the given warp to the default console output.
        /// </summary>
        public static void Dump(this VelocityWarp64 warp) =>
            Console.WriteLine(warp.ToString());

        public static VelocityWarp64 GetLaneIndexVector() =>
            VelocityWarp64.LaneIndexVector;

        #endregion

        #region Merge Operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 Merge(
            this VelocityWarp64 left,
            VelocityWarp64 right,
            VelocityWarp64 rightMask) =>
            new VelocityWarp64(
                Vector.ConditionalSelect(
                    rightMask.LowerAs<long>(),
                    right.LowerAs<long>(),
                    left.LowerAs<long>()),
                Vector.ConditionalSelect(
                    rightMask.UpperAs<long>(),
                    right.UpperAs<long>(),
                    left.UpperAs<long>()));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 MergeWithMask(
            this VelocityWarp64 left,
            VelocityWarp64 right,
            VelocityLaneMask rightMask)
        {
            var maskVector = VelocityWarp64.FromMask(rightMask);
            return Merge(left, right, maskVector);
        }

        #endregion

        #region Convert Operations

        /// <summary>
        /// Does not perform a conversion.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 ConvertNop(this VelocityWarp64 value) => value;

        /// <summary>
        /// Converts the given 64bit integer warp to a 64bit integer warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 ConvertIToI(this VelocityWarp64 value) =>
            ConvertNop(value);

        /// <summary>
        /// Converts the given 64bit unsigned integer warp to a 64bit unsigned integer
        /// warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 ConvertUToU(this VelocityWarp64 value) =>
            ConvertNop(value);

        /// <summary>
        /// Converts the given 64bit float warp to a 64bit float warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 ConvertFToF(this VelocityWarp64 value) =>
            ConvertNop(value);


        /// <summary>
        /// Converts the given 64bit integer warp to a 64bit unsigned integer warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 ConvertIToU(this VelocityWarp64 value) =>
            ConvertNop(value);

        /// <summary>
        /// Converts the given 64bit unsigned integer warp to a 64bit integer warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 ConvertUToI(this VelocityWarp64 value) =>
            ConvertNop(value);

        /// <summary>
        /// Converts the given 64bit integer warp to a 64bit float warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 ConvertIToF(this VelocityWarp64 value)
        {
            var lower = Vector.ConvertToDouble(value.LowerAs<long>());
            var upper = Vector.ConvertToDouble(value.UpperAs<long>());
            return new VelocityWarp64(lower, upper);
        }

        /// <summary>
        /// Converts the given 64bit unsigned integer warp to a 64bit float warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 ConvertUToF(this VelocityWarp64 value)
        {
            var lower = Vector.ConvertToDouble(value.LowerAs<ulong>());
            var upper = Vector.ConvertToDouble(value.UpperAs<ulong>());
            return new VelocityWarp64(lower, upper);
        }

        /// <summary>
        /// Converts the given 64bit float warp to a 64bit integer warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 ConvertFToI(this VelocityWarp64 value)
        {
            var lower = Vector.ConvertToInt64(value.LowerAs<double>());
            var upper = Vector.ConvertToInt64(value.UpperAs<double>());
            return new VelocityWarp64(lower, upper);
        }

        /// <summary>
        /// Converts the given 64bit float warp to a 64bit unsigned integer warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 ConvertFToU(this VelocityWarp64 value)
        {
            var lower = Vector.ConvertToUInt64(value.LowerAs<double>());
            var upper = Vector.ConvertToUInt64(value.UpperAs<double>());
            return new VelocityWarp64(lower, upper);
        }

        /// <summary>
        /// Narrows the given warp to a 32bit int warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 NarrowI(this VelocityWarp64 warp) =>
            Vector.Narrow(warp.LowerAs<long>(), warp.UpperAs<long>());

        /// <summary>
        /// Narrows the given warp to a 32bit uint warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 NarrowU(this VelocityWarp64 warp) =>
            Vector.Narrow(warp.LowerAs<ulong>(), warp.UpperAs<ulong>());

        /// <summary>
        /// Narrows the given warp to a 32bit float uint warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp32 NarrowF(this VelocityWarp64 warp) =>
            Vector.Narrow(warp.LowerAs<double>(), warp.UpperAs<double>());

        #endregion

        #region Binary Operations

        public static VelocityWarp64 ComputeRemI(
            this VelocityWarp64 left,
            VelocityWarp64 right) =>
            SubI(left, MulU(DivI(left, right), right));

        public static VelocityWarp64 ComputeRemU(
            this VelocityWarp64 left,
            VelocityWarp64 right) =>
            SubU(left, MulU(DivU(left, right), right));

        public static VelocityWarp64 ComputeRemF(
            this VelocityWarp64 left,
            VelocityWarp64 right) =>
            Sub(left, Abs(Mul(Div(left, right), right)));

        #endregion
    }

    partial class VelocityOperations
    {
        #region General 64bit Operations

        private readonly MethodInfo[] convertNarrowOperations64 = new MethodInfo[3]
        {
            GetMethod(
                typeof(VelocityWarpOperations64),
                nameof(VelocityWarpOperations64.NarrowI)),
            GetMethod(
                typeof(VelocityWarpOperations64),
                nameof(VelocityWarpOperations64.NarrowU)),
            GetMethod(
                typeof(VelocityWarpOperations64),
                nameof(VelocityWarpOperations64.NarrowF)),
        };

        private void InitVelocityOperations64(Type operationType64)
        {
            DumpMethod64 = GetMethod(
                operationType64,
                nameof(VelocityWarpOperations64.Dump));
            LaneIndexVectorOperation64 = GetMethod(
                operationType64,
                nameof(GetLaneIndexVector));

            MergeOperation64 = GetMethod(
                operationType64,
                nameof(VelocityWarpOperations64.Merge));
            MergeWithMaskOperation64 = GetMethod(
                operationType64,
                nameof(VelocityWarpOperations64.MergeWithMask));

            FromMaskOperation64 = GetMethod(
                typeof(VelocityWarp64),
                nameof(VelocityWarp64.FromMask));
            ToMaskOperation64 = GetMethod(
                typeof(VelocityWarp64),
                nameof(VelocityWarp64.ToMask));
        }

        public MethodInfo DumpMethod64 { get; private set; }
        public MethodInfo LaneIndexVectorOperation64 { get; private set; }

        public MethodInfo MergeOperation64 { get; private set; }
        public MethodInfo MergeWithMaskOperation64 { get; private set; }

        public MethodInfo FromMaskOperation64 { get; private set; }
        public MethodInfo ToMaskOperation64 { get; private set; }

        public MethodInfo GetConvertNarrowOperation64(VelocityWarpOperationMode mode) =>
            convertNarrowOperations64[(int)mode];

        #endregion

        #region Constant Values

        private readonly MethodInfo[] constValueOperations64 = new MethodInfo[]
        {
            GetMethod(typeof(VelocityWarp64), nameof(VelocityWarp64.GetConstI)),
            GetMethod(typeof(VelocityWarp64), nameof(VelocityWarp64.GetConstU)),
            GetMethod(typeof(VelocityWarp64), nameof(VelocityWarp64.GetConstF)),
        };

        public MethodInfo GetConstValueOperation64(VelocityWarpOperationMode mode) =>
            constValueOperations64[(int)mode];

        #endregion
    }
}
