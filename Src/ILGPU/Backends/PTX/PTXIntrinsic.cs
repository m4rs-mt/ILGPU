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
        /// Constructs a new PTX intrinsic that can handle all architectures.
        /// </summary>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="mode">The code-generation mode.</param>
        /// <param name="architecture">The target architecture (if any).</param>
        public PTXIntrinsic(
            Type handlerType,
            IntrinsicImplementationMode mode,
            PTXArchitecture architecture)
            : this(handlerType, mode)
        {
            MinArchitecture = architecture;
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
        /// <param name="architecture">The target architecture (if any).</param>
        public PTXIntrinsic(
            Type handlerType,
            string methodName,
            IntrinsicImplementationMode mode,
            PTXArchitecture architecture)
            : base(
                  BackendType.PTX,
                  handlerType,
                  methodName,
                  mode)
        {
            MinArchitecture = architecture;
        }

        /// <summary>
        /// Constructs a new PTX intrinsic.
        /// </summary>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="methodName">The target method name (or null).</param>
        /// <param name="mode">The code-generator mode.</param>
        /// <param name="minArchitecture">The min architecture (if any).</param>
        /// <param name="maxArchitecture">The max architecture.</param>
        public PTXIntrinsic(
            Type handlerType,
            string methodName,
            IntrinsicImplementationMode mode,
            PTXArchitecture? minArchitecture,
            PTXArchitecture maxArchitecture)
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
        public PTXArchitecture? MinArchitecture { get; }

        /// <summary>
        /// Returns the associated architecture (if any).
        /// </summary>
        public PTXArchitecture? MaxArchitecture { get; }

        #endregion

        #region Methods

        /// <summary cref="IntrinsicImplementation.CanHandleBackend(Backend)"/>
        protected internal override bool CanHandleBackend(Backend backend) =>
            backend is PTXBackend ptxBackend
            ? MinArchitecture.HasValue &&
                ptxBackend.Architecture < MinArchitecture.Value
                ? false
                : !MaxArchitecture.HasValue ||
                    ptxBackend.Architecture <= MaxArchitecture.Value
            : false;

        #endregion
    }
}
