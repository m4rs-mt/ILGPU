// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CPUKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using System;
using System.Reflection;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a single CPU kernel.
    /// </summary>
    public sealed class CPUKernel : Kernel
    {
        #region Static

        /// <summary>
        /// Represents the <see cref="KernelExecutionDelegate"/> property getter.
        /// </summary>
        internal static readonly MethodInfo GetKernelExecutionDelegate =
            typeof(CPUKernel).GetProperty(
                    nameof(KernelExecutionDelegate),
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .GetGetMethod(true);

        #endregion

        #region Instance

        /// <summary>
        /// Loads a compiled kernel into the given Cuda context as kernel program.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="kernel">The source kernel.</param>
        /// <param name="launcher">The launcher method for the given kernel.</param>
        /// <param name="kernelExecutionDelegate">The execution method.</param>
        internal CPUKernel(
            CPUAccelerator accelerator,
            CompiledKernel kernel,
            MethodInfo launcher,
            CPUKernelExecutionHandler kernelExecutionDelegate)
            : base(accelerator, kernel, launcher)
        {
            KernelExecutionDelegate = kernelExecutionDelegate
                                      ?? throw new ArgumentNullException(
                                          nameof(kernelExecutionDelegate));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated CPU runtime.
        /// </summary>
        public CPUAccelerator CPUAccelerator => Accelerator as CPUAccelerator;

        /// <summary>
        /// Returns the associated kernel-execution delegate.
        /// </summary>
        internal CPUKernelExecutionHandler KernelExecutionDelegate { get; }

        #endregion

        #region IDisposable

        /// <summary>
        /// Does not perform any operation.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing) { }

        #endregion
    }
}
