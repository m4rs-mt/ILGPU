﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CodeGeneratorBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.Runtime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a backend that works on several code generators and kernel builders
    /// in parallel to speed up code generation.
    /// </summary>
    /// <typeparam name="TDelegate">
    /// The intrinsic delegate type for backend implementations.
    /// </typeparam>
    /// <typeparam name="T">The main data type.</typeparam>
    /// <typeparam name="TCodeGenerator">The code-generator type.</typeparam>
    /// <typeparam name="TKernelBuilder">The kernel-builder type.</typeparam>
    public abstract class CodeGeneratorBackend<
        TDelegate,
        T,
        TCodeGenerator,
        TKernelBuilder> : Backend<TDelegate>
        where TDelegate : Delegate
        where TCodeGenerator : class, IBackendCodeGenerator<TKernelBuilder>
    {
        #region Instance

        /// <summary>
        /// Constructs a new generic backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="capabilities">The supported capabilities.</param>
        /// <param name="backendType">The backend type.</param>
        /// <param name="argumentMapper">The argument mapper to use.</param>
        protected CodeGeneratorBackend(
            Context context,
            CapabilityContext capabilities,
            BackendType backendType,
            ArgumentMapper argumentMapper)
            : base(
                  context,
                  capabilities,
                  backendType,
                  argumentMapper)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Compiles a new kernel using parallel processing.
        /// </summary>
        /// <param name="entryPoint"></param>
        /// <param name="backendContext"></param>
        /// <param name="specialization"></param>
        /// <returns></returns>
        protected sealed override CompiledKernel Compile(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization)
        {
            // Initialize the main builder and generator
            var mainBuilder = CreateKernelBuilder(
                entryPoint,
                backendContext,
                specialization,
                out T data);

            var generators = new List<TCodeGenerator>(backendContext.Count)
            {
                CreateKernelCodeGenerator(
                    backendContext.SharedAllocations,
                    backendContext.KernelMethod,
                    backendContext.KernelAllocas,
                    data)
            };

            // Create all remaining builders and code generators
            foreach (var (method, allocas) in backendContext)
            {
                generators.Add(
                    CreateFunctionCodeGenerator(
                        method,
                        allocas,
                        data));
            }

            // Generate code
            Parallel.For(0, generators.Count, i => generators[i].GenerateCode());

            // Generate all constants
            for (int i = 0, e = generators.Count; i < e; ++i)
                generators[i].GenerateConstants(mainBuilder);

            // Declare all methods
            for (int i = 0, e = generators.Count; i < e; ++i)
                generators[i].GenerateHeader(mainBuilder);

            // Merge all code generators in reverse order
            for (int i = generators.Count - 1; i >= 0; --i)
                generators[i].Merge(mainBuilder);

            // Create final kernel
            return CreateKernel(
                entryPoint,
                backendContext.KernelInfo,
                mainBuilder,
                data);
        }

        /// <summary>
        /// Creates the main kernel builder and initializes
        /// all required information.
        /// </summary>
        /// <param name="entryPoint">The current entry point.</param>
        /// <param name="backendContext">The backend context.</param>
        /// <param name="specialization">The backend specialization.</param>
        /// <param name="data">The user-defined data instance.</param>
        /// <returns>The resulting kernel builder.</returns>
        protected abstract TKernelBuilder CreateKernelBuilder(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization,
            out T data);

        /// <summary>
        /// Creates a new function-code generator.
        /// </summary>
        /// <param name="method">The current method.</param>
        /// <param name="allocas">The associated allocations.</param>
        /// <param name="data">The user-defined data instance.</param>
        /// <returns>The created function-code generator.</returns>
        protected abstract TCodeGenerator CreateFunctionCodeGenerator(
            Method method,
            Allocas allocas,
            T data);

        /// <summary>
        /// Creates a new kernel-code generator.
        /// </summary>
        /// <param name="sharedAllocations">All shared allocations.</param>
        /// <param name="method">The current method.</param>
        /// <param name="allocas">The associated allocations.</param>
        /// <param name="data">The user-defined data instance.</param>
        /// <returns>The created kernel-code generator.</returns>
        protected abstract TCodeGenerator CreateKernelCodeGenerator(
            in AllocaKindInformation sharedAllocations,
            Method method,
            Allocas allocas,
            T data);

        /// <summary>
        /// Creates the final compiled kernel instance.
        /// </summary>
        /// <param name="entryPoint">The current entry point.</param>
        /// <param name="builder">The kernel builder.</param>
        /// <param name="kernelInfo">Detailed information about the kernel.</param>
        /// <param name="data">The user-defined data instance.</param>
        /// <returns>The resulting compiled kernel.</returns>
        protected abstract CompiledKernel CreateKernel(
            EntryPoint entryPoint,
            CompiledKernel.KernelInfo kernelInfo,
            TKernelBuilder builder,
            T data);

        #endregion
    }
}
