// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: ILCompiledKernel.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Runtime.CPU;
using System;
using System.Collections.Immutable;
using System.Reflection;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// Represents a compiled kernel in MSIL form.
    /// </summary>
    public sealed class ILCompiledKernel : CompiledKernel
    {
        #region Instance

        /// <summary>
        /// Constructs a new IL compiled kernel.
        /// </summary>
        /// <param name="context">The associated context.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="kernelMethod">The main kernel method.</param>
        /// <param name="taskType">The custom task type.</param>
        /// <param name="taskConstructor">The custom task constructor.</param>
        /// <param name="taskArgumentMapping">Mapping of argument indices to fields.</param>
        internal ILCompiledKernel(
            Context context,
            EntryPoint entryPoint,
            MethodInfo kernelMethod,
            Type taskType,
            ConstructorInfo taskConstructor,
            ImmutableArray<FieldInfo> taskArgumentMapping)
            : base(context, entryPoint)
        {
            KernelMethod = kernelMethod;
            ExecutionHandler = (CPUKernelExecutionHandler)KernelMethod.CreateDelegate(
                typeof(CPUKernelExecutionHandler));
            TaskType = taskType;
            TaskConstructor = taskConstructor;
            TaskArgumentMapping = taskArgumentMapping;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the main kernel method.
        /// </summary>
        public MethodInfo KernelMethod { get; }

        /// <summary>
        /// Returns a CPU-runtime compatible execution handler.
        /// </summary>
        public CPUKernelExecutionHandler ExecutionHandler { get; }

        /// <summary>
        /// Returns the custom task type to dispatch the kernel.
        /// </summary>
        internal Type TaskType { get; }

        /// <summary>
        /// Returns the task constructor to instantiate the custom task type.
        /// </summary>
        internal ConstructorInfo TaskConstructor { get; }

        /// <summary>
        /// Returns a mapping of argument indices to fields.
        /// </summary>
        internal ImmutableArray<FieldInfo> TaskArgumentMapping { get; }

        #endregion
    }
}
