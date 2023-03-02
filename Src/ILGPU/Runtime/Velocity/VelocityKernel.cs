// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.Velocity;
using ILGPU.Util;
using System.Reflection;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// Represents a single Velocity kernel.
    /// </summary>
    public sealed class VelocityKernel : Kernel
    {
        #region Static

        /// <summary>
        /// Represents the <see cref="VelocityAccelerator"/> property getter.
        /// </summary>
        internal static readonly MethodInfo GetVelocityAccelerator =
            typeof(VelocityKernel).GetProperty(
                    nameof(VelocityAccelerator),
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .AsNotNull()
                .GetGetMethod(true)
                .AsNotNull();

        /// <summary>
        /// Represents the <see cref="KernelEntryPoint"/> property getter.
        /// </summary>
        internal static readonly MethodInfo GetKernelExecutionDelegate =
            typeof(VelocityKernel).GetProperty(
                nameof(KernelEntryPoint),
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .AsNotNull()
            .GetGetMethod(true)
            .AsNotNull();

        #endregion

        #region Instance

        /// <summary>
        /// Loads a compiled kernel into the given Cuda context as kernel program.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="kernel">The source kernel.</param>
        /// <param name="launcher">The launcher method for the given kernel.</param>
        internal VelocityKernel(
            VelocityAccelerator accelerator,
            VelocityCompiledKernel kernel,
            MethodInfo launcher)
            : base(accelerator, kernel, launcher)
        {
            KernelEntryPoint = kernel.CreateKernelEntryPoint();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated Velocity runtime.
        /// </summary>
        public VelocityAccelerator VelocityAccelerator =>
            Accelerator.AsNotNullCast<VelocityAccelerator>();

        /// <summary>
        /// The main kernel entry point function to be called from each velocity
        /// multiprocessor during execution.
        /// </summary>
        internal VelocityEntryPointHandler KernelEntryPoint { get; }

        #endregion

        #region IDisposable

        /// <summary>
        /// Does not perform any operation.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing) { }

        #endregion
    }
}
