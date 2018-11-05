// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: SkipCodeGenerationDefaultILBackend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using System.Reflection;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// The default IL backend that uses the original kernel method.
    /// However, it does not generate general IR code (debugging purposes).
    /// </summary>
    sealed class SkipCodeGenerationDefaultILBackend : DefaultILBackend
    {
        #region Constants

        /// <summary>
        /// The default amount of shared memory per kernel in bytes.
        /// </summary>
        /// <remarks>
        /// Note that this amount is only valid in the scope of the <see cref="ContextFlags.SkipCPUCodeGeneration"/>
        /// setting.
        /// </remarks>
        public const int SharedMemoryPerKernel = 1024 * 1024;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new IL backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        internal SkipCodeGenerationDefaultILBackend(Context context)
            : base(context)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Compiles a given compile unit with the specified entry point using
        /// the given kernel specialization.
        /// </summary>
        /// <typeparam name="TBackendHandler">The backend handler type.</typeparam>
        /// <param name="entry">The desired entry point.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="backendHandler">The backend handler.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        public override CompiledKernel Compile<TBackendHandler>(
            MethodInfo entry,
            in KernelSpecialization specialization,
            TBackendHandler backendHandler)
        {
            // Construct a new debugging entry point
            var entryPoint = new EntryPoint(
                entry,
                SharedMemoryPerKernel,
                specialization);

            // Note that we do not need an ABI and a valid backend context in this case
            return Compile(entryPoint, null, default, specialization);
        }

        #endregion
    }
}
