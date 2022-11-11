// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLIntrinsic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Intrinsics;
using System;
using System.Reflection;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Represents a specific handler for user defined code-generation functionality
    /// that is compatible with the <see cref="CLBackend"/>.
    /// </summary>
    public sealed class CLIntrinsic : IntrinsicImplementation
    {
        #region Nested Types

        /// <summary>
        /// Represents the handler delegate type of custom code-generation handlers.
        /// </summary>
        /// <param name="backend">The current backend.</param>
        /// <param name="codeGenerator">The code generator.</param>
        /// <param name="value">The value to generate code for.</param>
        public delegate void Handler(
            CLBackend backend,
            CLCodeGenerator codeGenerator,
            Value value);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new OpenCL intrinsic that can handle all architectures.
        /// </summary>
        /// <param name="targetMethod">The associated target method.</param>
        /// <param name="mode">The code-generation mode.</param>
        public CLIntrinsic(MethodInfo targetMethod, IntrinsicImplementationMode mode)
            : base(
                  BackendType.OpenCL,
                  targetMethod,
                  mode)
        { }

        /// <summary>
        /// Constructs a new OpenCL intrinsic that can handle all architectures.
        /// </summary>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="mode">The code-generation mode.</param>
        public CLIntrinsic(Type handlerType, IntrinsicImplementationMode mode)
            : base(
                  BackendType.OpenCL,
                  handlerType,
                  null,
                  mode)
        { }

        /// <summary>
        /// Constructs a new OpenCL intrinsic that can handle all architectures.
        /// </summary>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="methodName">The target method name (or null).</param>
        /// <param name="mode">The code-generator mode.</param>
        public CLIntrinsic(
            Type handlerType,
            string methodName,
            IntrinsicImplementationMode mode)
            : base(
                  BackendType.OpenCL,
                  handlerType,
                  methodName,
                  mode)
        { }

        #endregion

        #region Methods

        /// <summary cref="IntrinsicImplementation.CanHandleBackend(Backend)"/>
        protected internal override bool CanHandleBackend(Backend backend) =>
            backend is CLBackend;

        #endregion
    }
}
