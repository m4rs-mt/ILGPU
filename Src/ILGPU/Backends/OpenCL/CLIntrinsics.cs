// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.AtomicOperations;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Values;
using System;

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
                AtomicKind.Add,
                BasicValueType.Float64,
                CreateIntrinsic(
                    nameof(AtomicAddF64),
                    IntrinsicImplementationMode.Redirect));
        }

        #endregion

        #region Atomics

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
        private static void AtomicAddF32(ref float target, float value) =>
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
        private static void AtomicAddF64(ref double target, double value) =>
            Atomic.MakeAtomic(
                ref target,
                value,
                new AddDouble(),
                new CompareExchangeDouble());

        #endregion
    }
}
