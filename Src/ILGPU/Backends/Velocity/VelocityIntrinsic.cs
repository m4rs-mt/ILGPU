// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityIntrinsic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Intrinsics;
using System;
using System.Reflection;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// Represents a specific handler for user defined code-generation functionality
    /// that is compatible with the <see cref="VelocityBackend{TILEmitter}"/>.
    /// </summary>
    public sealed class VelocityIntrinsic : IntrinsicImplementation
    {
        #region Instance

        /// <summary>
        /// Constructs a new Velocity intrinsic.
        /// </summary>
        /// <param name="targetMethod">The associated target method.</param>
        /// <param name="mode">The code-generation mode.</param>
        public VelocityIntrinsic(
            MethodInfo targetMethod,
            IntrinsicImplementationMode mode)
            : base(
                  BackendType.Velocity,
                  targetMethod,
                  mode)
        { }

        /// <summary>
        /// Constructs a new Velocity intrinsic.
        /// </summary>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="methodName">The target method name (or null).</param>
        /// <param name="mode">The code-generation mode.</param>
        public VelocityIntrinsic(
            Type handlerType,
            string methodName,
            IntrinsicImplementationMode mode)
            : base(
                  BackendType.Velocity,
                  handlerType,
                  methodName,
                  mode)
        { }

        #endregion

        #region Methods

        /// <summary cref="IntrinsicImplementation.CanHandleBackend(Backend)"/>
        protected internal override bool CanHandleBackend(Backend backend) =>
            backend.BackendType == BackendType.Velocity;

        #endregion
    }
}
