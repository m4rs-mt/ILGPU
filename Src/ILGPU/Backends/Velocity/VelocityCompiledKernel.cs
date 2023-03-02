// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityCompiledKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Runtime.Velocity;
using System;
using System.Collections.Immutable;
using System.Reflection;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// Represents a compiled kernel in vectorized MSIL form.
    /// </summary>
    public sealed class VelocityCompiledKernel : CompiledKernel
    {
        #region Instance

        /// <summary>
        /// Constructs a new IL compiled kernel.
        /// </summary>
        /// <param name="context">The associated context.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="kernelMethod">The main kernel method.</param>
        /// <param name="parametersType">The custom parameters type.</param>
        /// <param name="parametersTypeConstructor">
        /// The type constructor of the parameters type.
        /// </param>
        /// <param name="parameterFields">
        /// Mapping of kernel parameter indices to parameter fields.
        /// </param>
        /// <param name="allocatedSharedMemorySize">
        /// The amount of statically allocated bytes of shared memory.
        /// </param>
        internal VelocityCompiledKernel(
            Context context,
            EntryPoint entryPoint,
            MethodInfo kernelMethod,
            Type parametersType,
            ConstructorInfo parametersTypeConstructor,
            ImmutableArray<FieldInfo> parameterFields,
            int allocatedSharedMemorySize)
            : base(context, entryPoint, null)
        {
            KernelMethod = kernelMethod;
            ParametersType = parametersType;
            ParameterFields = parameterFields;
            ParametersTypeConstructor = parametersTypeConstructor;
            AllocatedSharedMemorySize = allocatedSharedMemorySize;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the main kernel method.
        /// </summary>
        public MethodInfo KernelMethod { get; }

        /// <summary>
        /// Returns the custom parameter store type to dispatch the kernel.
        /// </summary>
        internal Type ParametersType { get; }

        /// <summary>
        /// Returns the type constructor to instantiate the custom parameters type.
        /// </summary>
        internal ConstructorInfo ParametersTypeConstructor { get; }

        /// <summary>
        /// Returns a mapping of kernel parameter indices to parameter field.s
        /// </summary>
        internal ImmutableArray<FieldInfo> ParameterFields { get; }

        /// <summary>
        /// Returns the size of statically allocated shared memory in bytes.
        /// </summary>
        public int AllocatedSharedMemorySize { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new kernel entry point to be used with this kernel module.
        /// </summary>
        /// <returns>A kernel entry point delegate.</returns>
        internal VelocityKernelEntryPoint CreateKernelEntryPoint() =>
            KernelMethod.CreateDelegate<VelocityKernelEntryPoint>();

        #endregion
    }
}

