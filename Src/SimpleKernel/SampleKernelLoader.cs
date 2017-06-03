// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                   Copyright (c) 2017 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: SampleKernelLoader.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SimpleKernel
{
    /// <summary>
    /// Represents a sample kernel loader.
    /// </summary>
    public class SampleKernelLoader : DisposeBase
    {
        #region Instance

        private readonly List<Kernel> kernels = new List<Kernel>();

        #endregion

        #region Methods

        /// <summary>
        /// Compiles and launches the specified method on the given accelerator.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        /// <param name="method">The target method to compile and launch.</param>
        /// <param name="flags">Custom compile-unit flags.</param>
        /// <param name="launcher">The custom kernel launcher.</param>
        public void CompileAndLaunchKernel(
            Accelerator accelerator,
            MethodInfo method,
            CompileUnitFlags flags,
            Action<Kernel> launcher)
        {
            // Create a backend for this device
            using (var backend = accelerator.CreateBackend())
            {
                // Create a new compile unit using the created backend
                using (var compileUnit = accelerator.Context.CreateCompileUnit(backend, flags))
                {
                    // Resolve and compile method into a kernel
                    var compiledKernel = backend.Compile(compileUnit, method);
                    // Info: use compiledKernel.GetBuffer() to retrieve the compiled kernel program data

                    // Load and initialize kernel on the target accelerator
                    var kernel = accelerator.LoadAutoGroupedKernel(compiledKernel);

                    launcher(kernel);

                    kernels.Add(kernel);
                }
            }
        }

        /// <summary>
        /// Compiles and launches the specified method on the given accelerator.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        /// <param name="method">The target method to compile and launch.</param>
        /// <param name="launcher">The custom kernel launcher.</param>
        public void CompileAndLaunchKernel(
            Accelerator accelerator,
            MethodInfo method,
            Action<Kernel> launcher)
        {
            CompileAndLaunchKernel(
                accelerator,
                method,
                CompileUnitFlags.None,
                launcher);
        }

        #endregion

        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            foreach (var kernel in kernels)
                kernel.Dispose();
            kernels.Clear();
        }

        #endregion
    }
}
