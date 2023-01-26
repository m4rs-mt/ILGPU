// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityWarpVerifier.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// Represents a sanity checker for warp operations.
    /// </summary>
    internal interface IVelocityWarpVerifier
    {
        /// <summary>
        /// Verifies broadcast operations.
        /// </summary>
        /// <param name="sourceLane">The source lane vector.</param>
        void VerifyBroadcast(VelocityWarp32 sourceLane);

        /// <summary>
        /// Verifies broadcast operations.
        /// </summary>
        /// <param name="sourceLane">The uniform source lane value.</param>
        void VerifyBroadcast(uint sourceLane);

        /// <summary>
        /// Verifies shuffle operations.
        /// </summary>
        /// <param name="sourceLanes">The source lanes vector.</param>
        void VerifyShuffle(VelocityWarp32 sourceLanes);

        /// <summary>
        /// Verifies shuffle down operations.
        /// </summary>
        /// <param name="delta">The delta lanes vector.</param>
        /// <param name="width">The width vector.</param>
        void VerifyShuffleDown(VelocityWarp32 delta, VelocityWarp32 width);

        /// <summary>
        /// Verifies shuffle up operations.
        /// </summary>
        /// <param name="delta">The delta lanes vector.</param>
        /// <param name="width">The width vector.</param>
        void VerifyShuffleUp(VelocityWarp32 delta, VelocityWarp32 width);

        /// <summary>
        /// Verifies shuffle xor operations.
        /// </summary>
        /// <param name="delta">The delta lanes vector.</param>
        /// <param name="width">The width vector.</param>
        void VerifyShuffleXor(VelocityWarp32 delta, VelocityWarp32 width);
    }

    /// <summary>
    /// Default implementations for the <see cref="IVelocityWarpVerifier"/> interface.
    /// </summary>
    static class VelocityWarpVerifier
    {
        public static TVerifier GetVerifier<TVerifier>()
            where TVerifier : struct, IVelocityWarpVerifier => default;

        /// <summary>
        /// Does not verify any values.
        /// </summary>
        public readonly struct Disabled : IVelocityWarpVerifier
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void VerifyBroadcast(VelocityWarp32 sourceLane)
            { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void VerifyBroadcast(uint sourceLane)
            { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void VerifyShuffle(VelocityWarp32 sourceLanes)
            { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void VerifyShuffleDown(VelocityWarp32 delta, VelocityWarp32 width)
            { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void VerifyShuffleUp(VelocityWarp32 delta, VelocityWarp32 width)
            { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void VerifyShuffleXor(VelocityWarp32 delta, VelocityWarp32 width)
            { }
        }
    }
}
