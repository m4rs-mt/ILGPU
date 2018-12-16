// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: WarpIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    enum WarpIntrinsicKind
    {
        Shuffle = ShuffleKind.Generic,
        ShuffleDown = ShuffleKind.Down,
        ShuffleUp = ShuffleKind.Up,
        ShuffleXor = ShuffleKind.Xor,

        SubShuffle,
        SubShuffleDown,
        SubShuffleUp,
        SubShuffleXor,

        Barrier,
        WarpSize,
        LaneIdx,
    }

    /// <summary>
    /// Marks warp methods that are builtin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class WarpIntrinsicAttribute : IntrinsicAttribute
    {
        public WarpIntrinsicAttribute(WarpIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.Warp;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public WarpIntrinsicKind IntrinsicKind { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles warp operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleWarpOperation(
            in InvocationContext context,
            WarpIntrinsicAttribute attribute)
        {
            var builder = context.Builder;
            switch (attribute.IntrinsicKind)
            {
                case WarpIntrinsicKind.Shuffle:
                case WarpIntrinsicKind.ShuffleDown:
                case WarpIntrinsicKind.ShuffleUp:
                case WarpIntrinsicKind.ShuffleXor:
                    return builder.CreateShuffle(
                        context[0],
                        context[1],
                        (ShuffleKind)attribute.IntrinsicKind);
                case WarpIntrinsicKind.SubShuffle:
                case WarpIntrinsicKind.SubShuffleDown:
                case WarpIntrinsicKind.SubShuffleUp:
                case WarpIntrinsicKind.SubShuffleXor:
                    return builder.CreateShuffle(
                        context[0],
                        context[1],
                        context[2],
                        (ShuffleKind)(attribute.IntrinsicKind - WarpIntrinsicKind.SubShuffle));
                case WarpIntrinsicKind.Barrier:
                    return builder.CreateBarrier(BarrierKind.WarpLevel);
                case WarpIntrinsicKind.WarpSize:
                    return builder.CreateWarpSizeValue();
                case WarpIntrinsicKind.LaneIdx:
                    return builder.CreateLaneIdxValue();
                default:
                    throw new NotSupportedException("Invalid warp operation");
            }
        }
    }
}
