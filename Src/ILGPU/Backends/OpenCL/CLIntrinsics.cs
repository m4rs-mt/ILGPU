// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.AtomicOperations;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Values;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Implements and initializes OpenCL intrinsics.
    /// </summary>
    static partial class CLIntrinsics
    {
        #region Specializers

        /// <summary>
        /// The CLIntrinsics type.
        /// </summary>
        private static readonly Type CLIntrinsicsType = typeof(CLIntrinsics);

        /// <summary>
        /// Creates a new CL intrinsic.
        /// </summary>
        /// <param name="name">The name of the intrinsic.</param>
        /// <param name="mode">The implementation mode.</param>
        /// <returns>The created intrinsic.</returns>
        private static CLIntrinsic CreateIntrinsic(
            string name,
            IntrinsicImplementationMode mode) =>
            new CLIntrinsic(
                CLIntrinsicsType,
                name,
                mode);

        /// <summary>
        /// Registers all CL intrinsics with the given manager.
        /// </summary>
        /// <param name="manager">The target implementation manager.</param>
        public static void Register(IntrinsicImplementationManager manager)
        {
            // Atomics
            manager.RegisterGenericAtomic(
                AtomicKind.Add,
                BasicValueType.Float32,
                CreateIntrinsic(
                    nameof(AtomicAddF32),
                    IntrinsicImplementationMode.Redirect));
            manager.RegisterGenericAtomic(
                AtomicKind.Min,
                BasicValueType.Float32,
                CreateIntrinsic(
                    nameof(AtomicMinF32),
                    IntrinsicImplementationMode.Redirect));
            manager.RegisterGenericAtomic(
                AtomicKind.Max,
                BasicValueType.Float32,
                CreateIntrinsic(
                    nameof(AtomicMaxF32),
                    IntrinsicImplementationMode.Redirect));
            manager.RegisterGenericAtomic(
                AtomicKind.Add,
                BasicValueType.Float64,
                CreateIntrinsic(
                    nameof(AtomicAddF64),
                    IntrinsicImplementationMode.Redirect));
            manager.RegisterGenericAtomic(
                AtomicKind.Min,
                BasicValueType.Float64,
                CreateIntrinsic(
                    nameof(AtomicMinF64),
                    IntrinsicImplementationMode.Redirect));
            manager.RegisterGenericAtomic(
                AtomicKind.Max,
                BasicValueType.Float64,
                CreateIntrinsic(
                    nameof(AtomicMaxF64),
                    IntrinsicImplementationMode.Redirect));

            // Group
            manager.RegisterPredicateBarrier(
                PredicateBarrierKind.PopCount,
                CreateIntrinsic(
                    nameof(BarrierPopCount),
                    IntrinsicImplementationMode.Redirect));
        }

        #endregion

        #region Atomics

        /// <summary>
        /// Represents an atomic min operation in software.
        /// </summary>
        private readonly struct MinFloat : IAtomicOperation<float>
        {
            public float Operation(float current, float value) =>
                IntrinsicMath.Min(current, value);
        }

        /// <summary>
        /// A software implementation for atomic max on 32-bit floats.
        /// </summary>
        /// <param name="target">The target address.</param>
        /// <param name="value">The value to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float AtomicMinF32(ref float target, float value) =>
            Atomic.MakeAtomic(
                ref target,
                value,
                new MinFloat(),
                new CompareExchangeFloat());

        /// <summary>
        /// Represents an atomic max operation in software.
        /// </summary>
        private readonly struct MaxFloat : IAtomicOperation<float>
        {
            public float Operation(float current, float value) =>
                IntrinsicMath.Max(current, value);
        }

        /// <summary>
        /// A software implementation for atomic max on 32-bit floats.
        /// </summary>
        /// <param name="target">The target address.</param>
        /// <param name="value">The value to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float AtomicMaxF32(ref float target, float value) =>
            Atomic.MakeAtomic(
                ref target,
                value,
                new MaxFloat(),
                new CompareExchangeFloat());

        /// <summary>
        /// Represents an atomic min operation in software.
        /// </summary>
        private readonly struct MinDouble : IAtomicOperation<double>
        {
            public double Operation(double current, double value) =>
                IntrinsicMath.Min(current, value);
        }

        /// <summary>
        /// A software implementation for atomic max on 64-bit floats.
        /// </summary>
        /// <param name="target">The target address.</param>
        /// <param name="value">The value to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double AtomicMinF64(ref double target, double value) =>
            Atomic.MakeAtomic(
                ref target,
                value,
                new MinDouble(),
                new CompareExchangeDouble());

        /// <summary>
        /// Represents an atomic max operation in software.
        /// </summary>
        private readonly struct MaxDouble : IAtomicOperation<double>
        {
            public double Operation(double current, double value) =>
                IntrinsicMath.Max(current, value);
        }

        /// <summary>
        /// A software implementation for atomic max on 64-bit floats.
        /// </summary>
        /// <param name="target">The target address.</param>
        /// <param name="value">The value to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double AtomicMaxF64(ref double target, double value) =>
            Atomic.MakeAtomic(
                ref target,
                value,
                new MaxDouble(),
                new CompareExchangeDouble());

        /// <summary>
        /// Represents an atomic add operation of type float.
        /// </summary>
        private readonly struct AddFloat : IAtomicOperation<float>
        {
            public float Operation(float current, float value) => current + value;
        }

        /// <summary>
        /// A software implementation for atomic adds on 32-bit floats.
        /// </summary>
        /// <param name="target">The target address.</param>
        /// <param name="value">The value to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float AtomicAddF32(ref float target, float value) =>
            Atomic.MakeAtomic(
                ref target,
                value,
                new AddFloat(),
                new CompareExchangeFloat());

        /// <summary>
        /// Represents an atomic add operation of type double.
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double AtomicAddF64(ref double target, double value) =>
            Atomic.MakeAtomic(
                ref target,
                value,
                new AddDouble(),
                new CompareExchangeDouble());

        #endregion

        #region Groups

        /// <summary>
        /// A software implementation to simulate barriers with pop count.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BarrierPopCount(bool predicate)
        {
            ref var counter = ref SharedMemory.Allocate<int>();
            if (Group.IsFirstThread)
                counter = 0;
            Group.Barrier();
            if (predicate)
                Atomic.Add(ref counter, 1);
            Group.Barrier();
            return counter;
        }

        #endregion
    }
}
