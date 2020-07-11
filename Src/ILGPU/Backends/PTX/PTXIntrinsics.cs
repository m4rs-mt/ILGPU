﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.AtomicOperations;
using ILGPU.Frontend;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Values;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Implements and initializes PTX intrinsics.
    /// </summary>
    static partial class PTXIntrinsics
    {
        #region Debugging

        [External("__assertfail")]
        private static void AssertFail(
            string message,
            string file,
            int line,
            string function,
            int charSize)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertFailed(string message) =>
            AssertFail(message, "Kernel.cs", 0, "Kernel", 1);

        #endregion

        #region Specializers

        /// <summary>
        /// The PTXIntrinsics type.
        /// </summary>
        private static readonly Type PTXIntrinsicsType = typeof(PTXIntrinsics);

        /// <summary>
        /// The Half implementation type.
        /// </summary>
        private static readonly Type HalfType = typeof(HalfExtensions);

        /// <summary>
        /// Creates a new PTX intrinsic.
        /// </summary>
        /// <param name="name">The name of the intrinsic.</param>
        /// <param name="mode">The implementation mode.</param>
        /// <param name="minArchitecture">The minimum architecture.</param>
        /// <param name="maxArchitecture">The maximum architecture.</param>
        /// <returns>The created intrinsic.</returns>
        private static PTXIntrinsic CreateIntrinsic(
            string name,
            IntrinsicImplementationMode mode,
            PTXArchitecture? minArchitecture,
            PTXArchitecture maxArchitecture) =>
            new PTXIntrinsic(
                PTXIntrinsicsType,
                name,
                mode,
                minArchitecture,
                maxArchitecture);

        /// <summary>
        /// Creates a new PTX intrinsic.
        /// </summary>
        /// <param name="name">The name of the intrinsic.</param>
        /// <param name="mode">The implementation mode.</param>
        /// <returns>The created intrinsic.</returns>
        private static PTXIntrinsic CreateIntrinsic(
            string name,
            IntrinsicImplementationMode mode) =>
            new PTXIntrinsic(PTXIntrinsicsType, name, mode);

        private static PTXIntrinsic CreateFP16Intrinsic(
            string name,
            PTXArchitecture? maxArchitecture) =>
            maxArchitecture.HasValue
            ? new PTXIntrinsic(
                HalfType,
                name,
                IntrinsicImplementationMode.Redirect,
                null,
                maxArchitecture.Value)
            : new PTXIntrinsic(HalfType, name, IntrinsicImplementationMode.Redirect);

        /// <summary>
        /// Registers all PTX intrinsics with the given manager.
        /// </summary>
        /// <param name="manager">The target implementation manager.</param>
        public static void Register(IntrinsicImplementationManager manager)
        {
            RegisterAtomics(manager);
            RegisterBroadcasts(manager);
            RegisterWarpShuffles(manager);
            RegisterFP16(manager);

            // Register assert support
            manager.RegisterDebug(
                DebugKind.AssertFailed,
                CreateIntrinsic(
                    nameof(AssertFailed),
                    IntrinsicImplementationMode.Redirect));
        }

        #endregion

        #region Atomics

        /// <summary>
        /// Registers all atomic intrinsics with the given manager.
        /// </summary>
        /// <param name="manager">The target implementation manager.</param>
        private static void RegisterAtomics(IntrinsicImplementationManager manager) =>
            manager.RegisterGenericAtomic(
                AtomicKind.Add,
                BasicValueType.Float64,
                CreateIntrinsic(
                    nameof(AtomicAddF64),
                    IntrinsicImplementationMode.Redirect,
                    null,
                    PTXArchitecture.SM_53));

        /// <summary>
        /// Represents an atomic compare-exchange operation of type double.
        /// </summary>
        private readonly struct AddDouble : IAtomicOperation<double>
        {
            public double Operation(double current, double value) => current + value;
        }

        /// <summary>
        /// A software implementation for atomic adds on 64-bit floats.
        /// </summary>
        /// <param name="target">The target address.</param>
        /// <param name="value">The value to add.</param>
        private static double AtomicAddF64(ref double target, double value) =>
            Atomic.MakeAtomic(
                ref target,
                value,
                new AddDouble(),
                new CompareExchangeDouble());

        #endregion

        #region Broadcasts

        /// <summary>
        /// Registers all broadcast intrinsics with the given manager.
        /// </summary>
        /// <param name="manager">The target implementation manager.</param>
        private static void RegisterBroadcasts(
            IntrinsicImplementationManager manager)
        {
            manager.RegisterBroadcast(
                BroadcastKind.GroupLevel,
                CreateIntrinsic(
                    nameof(GroupBroadcast),
                    IntrinsicImplementationMode.Redirect));
            manager.RegisterBroadcast(
                BroadcastKind.WarpLevel,
                CreateIntrinsic(
                    nameof(WarpBroadcast),
                    IntrinsicImplementationMode.Redirect));
        }

        /// <summary>
        /// Implements a single group-broadcast operation.
        /// </summary>
        /// <typeparam name="T">The type to broadcast.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T GroupBroadcast<T>(T value, int groupIndex)
            where T : unmanaged
        {
            ref var sharedMemory = ref SharedMemory.Allocate<T>();
            if (Group.LinearIndex == groupIndex)
                sharedMemory = value;
            Group.Barrier();

            return sharedMemory;
        }

        /// <summary>
        /// Wraps a single warp-broadcast operation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T WarpBroadcast<T>(T value, int laneIndex)
            where T : unmanaged =>
            Warp.Shuffle(value, laneIndex);

        #endregion
    }
}
