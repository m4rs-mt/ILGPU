// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: ILContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.IL;
using ILGPU.IR.Intrinsics;
using System;

namespace ILGPU.Algorithms.IL
{
    /// <summary>
    /// Manages custom IL-specific intrinsics.
    /// </summary>
    static partial class ILContext
    {
        /// <summary>
        /// A wrapper implementation for IL intrinsics.
        /// </summary>
        sealed class ILIntrinsic : IntrinsicImplementation
        {
            public ILIntrinsic(
                Type handlerType,
                string methodName,
                IntrinsicImplementationMode mode)
                : base(BackendType.IL, handlerType, methodName, mode)
            { }

            /// <summary cref="IntrinsicImplementation.CanHandleBackend(Backend)"/>
            protected override bool CanHandleBackend(Backend backend) =>
                backend is ILBackend;
        }

        /// <summary>
        /// The <see cref="ILGroupExtensions"/> type.
        /// </summary>
        internal static readonly Type CPUGroupExtensionsType = typeof(ILGroupExtensions);

        /// <summary>
        /// The <see cref="ILWarpExtensions"/> type.
        /// </summary>
        internal static readonly Type CPUWarpExtensionsType = typeof(ILWarpExtensions);

        /// <summary>
        /// Registers an intrinsic mapping.
        /// </summary>
        /// <param name="manager">The current manager.</param>
        /// <param name="sourceType">The source type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="name">The method name to register.</param>
        private static void RegisterIntrinsicMapping(
            IntrinsicImplementationManager manager,
            Type sourceType,
            Type targetType,
            string name)
        {
            var sourceMethod = sourceType.GetMethod(name, AlgorithmContext.IntrinsicBindingFlags);
            manager.RegisterMethod(
                sourceMethod,
                new ILIntrinsic(targetType, name, IntrinsicImplementationMode.Redirect));
        }
    }
}
