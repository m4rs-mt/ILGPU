// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXIntrinsic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Intrinsics;
using ILGPU.Runtime.Cuda;
using System;
using System.Reflection;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents a specific handler for user defined code-generation functionality
    /// that is compatible with the <see cref="PTXBackend"/>.
    /// </summary>
    public sealed class PTXIntrinsic : IntrinsicImplementation
    {
        #region Nested Types

        /// <summary>
        /// Represents the handler delegate type of custom code-generation handlers.
        /// </summary>
        /// <param name="backend">The current backend.</param>
        /// <param name="codeGenerator">The code generator.</param>
        /// <param name="value">The value to generate code for.</param>
        public delegate void Handler(
            PTXBackend backend,
            PTXCodeGenerator codeGenerator,
            Value value);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new PTX intrinsic that can handle all architectures.
        /// </summary>
        /// <param name="targetMethod">The associated target method.</param>
        /// <param name="mode">The code-generation mode.</param>
        public PTXIntrinsic(MethodInfo targetMethod, IntrinsicImplementationMode mode)
            : base(
                  BackendType.PTX,
                  targetMethod,
                  mode)
        { }

        /// <summary>
        /// Constructs a new PTX intrinsic that can handle all architectures.
        /// </summary>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="mode">The code-generation mode.</param>
        public PTXIntrinsic(Type handlerType, IntrinsicImplementationMode mode)
            : base(
                  BackendType.PTX,
                  handlerType,
                  null,
                  mode)
        { }

        /// <summary>
        /// Constructs a new PTX intrinsic that can handle all architectures
        /// newer or equal to <paramref name="minArchitecture"/>.
        /// </summary>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="mode">The code-generation mode.</param>
        /// <param name="minArchitecture">The target/minimum architecture.</param>
        public PTXIntrinsic(
            Type handlerType,
            IntrinsicImplementationMode mode,
            CudaArchitecture minArchitecture)
            : this(handlerType, mode)
        {
            MinArchitecture = minArchitecture;
        }

        /// <summary>
        /// Constructs a new PTX intrinsic.
        /// </summary>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="methodName">The target method name (or null).</param>
        /// <param name="mode">The code-generator mode.</param>
        public PTXIntrinsic(
            Type handlerType,
            string methodName,
            IntrinsicImplementationMode mode)
            : base(
                  BackendType.PTX,
                  handlerType,
                  methodName,
                  mode)
        { }

        /// <summary>
        /// Constructs a new PTX intrinsic.
        /// </summary>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="methodName">The target method name (or null).</param>
        /// <param name="mode">The code-generator mode.</param>
        /// <param name="minArchitecture">The target/minimum architecture.</param>
        public PTXIntrinsic(
            Type handlerType,
            string methodName,
            IntrinsicImplementationMode mode,
            CudaArchitecture minArchitecture)
            : base(
                  BackendType.PTX,
                  handlerType,
                  methodName,
                  mode)
        {
            MinArchitecture = minArchitecture;
        }

        /// <summary>
        /// Constructs a new PTX intrinsic.
        /// </summary>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="methodName">The target method name (or null).</param>
        /// <param name="mode">The code-generator mode.</param>
        /// <param name="minArchitecture">The min architecture (if any).</param>
        /// <param name="maxArchitecture">The max architecture (exclusive).</param>
        public PTXIntrinsic(
            Type handlerType,
            string methodName,
            IntrinsicImplementationMode mode,
            CudaArchitecture? minArchitecture,
            CudaArchitecture maxArchitecture)
            : base(
                  BackendType.PTX,
                  handlerType,
                  methodName,
                  mode)
        {
            MinArchitecture = minArchitecture;
            MaxArchitecture = maxArchitecture;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated architecture (if any).
        /// </summary>
        /// <remarks>
        /// This intrinsic will be used for any architecture greater than or equal this
        /// value.
        /// </remarks>
        public CudaArchitecture? MinArchitecture { get; }

        /// <summary>
        /// Returns the associated architecture (if any).
        /// </summary>
        /// <remarks>
        /// This intrinsic will be used for any architecture less than this value.
        /// </remarks>
        public CudaArchitecture? MaxArchitecture { get; }

        #endregion

        #region Methods

        /// <summary cref="IntrinsicImplementation.CanHandleBackend(Backend)"/>
        protected internal override bool CanHandleBackend(Backend backend) =>
            backend is PTXBackend ptxBackend
            && (!MinArchitecture.HasValue ||
                ptxBackend.Architecture >= MinArchitecture.Value)
            && (!MaxArchitecture.HasValue ||
                    ptxBackend.Architecture < MaxArchitecture.Value);

        #endregion
    }
}
