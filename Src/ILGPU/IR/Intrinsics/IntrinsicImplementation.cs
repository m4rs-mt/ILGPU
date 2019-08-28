// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: IntrinsicImplementation.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Resources;
using System;
using System.Reflection;

namespace ILGPU.IR.Intrinsics
{
    /// <summary>
    /// Represents the handler mode of a custom handler routine.
    /// </summary>
    public enum IntrinsicImplementationMode
    {
        /// <summary>
        /// Indicates whether the associated method should be replaced by a
        /// different method.
        /// </summary>
        Redirect,

        /// <summary>
        /// Indicates whether the associated method has a custom code-generation
        /// module is invoked during code generation.
        /// </summary>
        GenerateCode
    }

    /// <summary>
    /// Represents an abstract intrinsic implementation.
    /// </summary>
    public abstract class IntrinsicImplementation : IIntrinsicImplementation
    {
        #region Instance

        /// <summary>
        /// Constructs a new implementation.
        /// </summary>
        /// <param name="backendType">The main backend type.</param>
        /// <param name="targetMethod">The associated target method.</param>
        /// <param name="mode">The code-generation mode.</param>
        protected IntrinsicImplementation(
            BackendType backendType,
            MethodInfo targetMethod,
            IntrinsicImplementationMode mode)
        {
            BackendType = backendType;
            TargetMethod = targetMethod ?? throw new NotSupportedException(
                string.Format(ErrorMessages.NotSupportedIntrinsic, GetType()));
            if (TargetMethod.IsGenericMethod)
                TargetMethod = TargetMethod.GetGenericMethodDefinition();
            Mode = mode;
        }

        /// <summary>
        /// Constructs a new implementation.
        /// </summary>
        /// <param name="backendType">The main backend type.</param>
        /// <param name="handlerType">The associated target handler type.</param>
        /// <param name="methodName">The target method name (or null).</param>
        /// <param name="mode">The code-generation mode.</param>
        protected IntrinsicImplementation(
            BackendType backendType,
            Type handlerType,
            string methodName,
            IntrinsicImplementationMode mode)
            : this(
                  backendType,
                  handlerType.GetMethod(
                      methodName ?? "Invoke",
                      BindingFlags.Public | BindingFlags.NonPublic |
                      BindingFlags.Static | BindingFlags.Instance),
                  mode)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated backend type.
        /// </summary>
        public BackendType BackendType { get; }

        /// <summary>
        /// Returns the associated code-generator mode.
        /// </summary>
        public IntrinsicImplementationMode Mode { get; }

        /// <summary>
        /// Returns the user-defined target method.
        /// </summary>
        public MethodInfo TargetMethod { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Determines whether the attribute is compatible with the given backend.
        /// </summary>
        /// <param name="backend">The current backend.</param>
        /// <returns>True, if the attribute is compatible with the given backend.</returns>
        public bool CanHandle(Backend backend) =>
            backend.BackendType == BackendType && CanHandleBackend(backend);

        /// <summary>
        /// Determines whether the attribute is compatible with the given backend.
        /// </summary>
        /// <param name="backend">The current backend.</param>
        /// <returns>True, if the attribute is compatible with the given backend.</returns>
        protected internal abstract bool CanHandleBackend(Backend backend);

        /// <summary>
        /// Resolves an intrinsic implementation for the current attribute.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <returns>The resolved intrinsic implementation.</returns>
        public IntrinsicMapping<TDelegate> ResolveMapping<TDelegate>()
            where TDelegate : Delegate =>
            new IntrinsicMapping<TDelegate>(this);

        #endregion
    }

    /// <summary>
    /// Marks methods that rely on an intrinsic-implementation provider during backend specialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [CLSCompliant(false)]
    public sealed class IntrinsicImplementationAttribute : Attribute { }
}
