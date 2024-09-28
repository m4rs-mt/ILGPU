// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using ILGPU.Resources;
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

        Broadcast,
    }

    /// <summary>
    /// Marks warp methods that are built in.
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
            ref InvocationContext context,
            WarpIntrinsicAttribute attribute)
        {
            var builder = context.Builder;
            var location = context.Location;
            switch (attribute.IntrinsicKind)
            {
                case WarpIntrinsicKind.Shuffle:
                case WarpIntrinsicKind.ShuffleDown:
                case WarpIntrinsicKind.ShuffleUp:
                case WarpIntrinsicKind.ShuffleXor:
                    return builder.CreateShuffle(
                        location,
                        context[0],
                        context[1],
                        (ShuffleKind)attribute.IntrinsicKind);
                case WarpIntrinsicKind.SubShuffle:
                case WarpIntrinsicKind.SubShuffleDown:
                case WarpIntrinsicKind.SubShuffleUp:
                case WarpIntrinsicKind.SubShuffleXor:
                    return builder.CreateShuffle(
                        location,
                        context[0],
                        context[1],
                        context[2],
                        (ShuffleKind)(
                            attribute.IntrinsicKind - WarpIntrinsicKind.SubShuffle));
                case WarpIntrinsicKind.Barrier:
                    return builder.CreateBarrier(location, BarrierKind.WarpLevel);
                case WarpIntrinsicKind.WarpSize:
                    return builder.CreateWarpSizeValue(location);
                case WarpIntrinsicKind.LaneIdx:
                    return builder.CreateLaneIdxValue(location);
                case WarpIntrinsicKind.Broadcast:
                    return builder.CreateBroadcast(
                        location,
                        context[0],
                        context[1],
                        BroadcastKind.WarpLevel);
                default:
                    throw context.Location.GetNotSupportedException(
                        ErrorMessages.NotSupportedWarpIntrinsic,
                        attribute.IntrinsicKind.ToString());
            }
        }
    }
}
