// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Backend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Util;
using ILGPUC.Backends.EntryPoints;
using ILGPUC.Frontend;
using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.Transformations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ILGPUC.Backends;

/// <summary>
/// Represents the general type of a backend.
/// </summary>
enum BackendType
{
    /// <summary>
    /// A PTX backend.
    /// </summary>
    PTX,
}

/// <summary>
/// Represents a general ILGPU backend.
/// </summary>
/// <remarks>
/// Constructs a new generic backend.
/// </remarks>
/// <param name="backendType">The backend type.</param>
/// <param name="acceleratorType">The accelerator type.</param>
/// <param name="capabilities">The supported capabilities.</param>
abstract class Backend(
    BackendType backendType,
    AcceleratorType acceleratorType,
    CapabilityContext capabilities) : DisposeBase
{
    /// <summary>
    /// Allocation information of kernels.
    /// </summary>
    internal sealed class Allocations
    {
        private readonly Dictionary<Method, Allocas> _allocations;

        /// <summary>
        /// Creates a new allocation instance.
        /// </summary>
        /// <param name="kernelContext">The kernel context.</param>
        /// <param name="kernelMethod">The entry point kernel method.</param>
        public Allocations(IRContext kernelContext, Method kernelMethod)
        {
            // Init dynamic allocations
            var dynamicAllocations =
                ImmutableArray.CreateBuilder<AllocaInformation>(4);

            // Compute alloca information
            _allocations = new Dictionary<Method, Allocas>(kernelContext.Methods.Count);

            foreach (var method in kernelContext.Methods)
            {
                var allocas = Allocas.Create(method.Blocks);
                _allocations.Add(method, allocas);

                foreach (var dynamicAllocation in allocas.DynamicSharedAllocations)
                    dynamicAllocations.Add(dynamicAllocation);
            }

            // Compute landscape to determine shared memory information
            var landscape = Landscape.Create(kernelContext.Methods);


            // Iterate through the call graph
            var allocations = _allocations;
            (CompiledKernelSharedMemoryMode Mode, int Shared, int Local) Traverse(
                Landscape.Entry entry,
                CompiledKernelSharedMemoryMode mode,
                int sharedMemSize,
                int localMemSize)
            {
                // Check shared memory status
                var allocas = allocations[entry.Method];
                localMemSize += allocas.LocalMemorySize;
                sharedMemSize += allocas.SharedMemorySize;

                switch (mode)
                {
                    case CompiledKernelSharedMemoryMode.Static when
                        allocas.DynamicSharedAllocations.Length > 0 && sharedMemSize < 1:
                        mode = CompiledKernelSharedMemoryMode.Dynamic;
                        break;
                    case CompiledKernelSharedMemoryMode.Static when
                        allocas.DynamicSharedAllocations.Length > 0 && sharedMemSize > 0:
                        mode = CompiledKernelSharedMemoryMode.Hybrid;
                        break;
                    case CompiledKernelSharedMemoryMode.Dynamic when
                        allocas.SharedAllocations.Length > 0:
                        mode = CompiledKernelSharedMemoryMode.Hybrid;
                        break;
                }

                // Recurse into each child
                foreach (var method in entry.References)
                {
                    var (newMode, newShared, newLocal) = Traverse(
                        landscape[method],
                        mode,
                        sharedMemSize,
                        localMemSize);

                    mode = (CompiledKernelSharedMemoryMode)Math.Max(
                        (int)mode,
                        (int)newMode);
                    sharedMemSize = Math.Max(sharedMemSize, newShared);
                    localMemSize = Math.Max(localMemSize, newLocal);
                }

                return (mode, sharedMemSize, localMemSize);
            }

            // Begin traversal
            var (mode, shared, local) = Traverse(
                landscape[kernelMethod],
                CompiledKernelSharedMemoryMode.Static,
                0,
                0);

            // Store results
            SharedMemoryMode = mode;
            SharedMemorySize = shared;
            LocalMemorySize = local;
            DynamicAllocations = dynamicAllocations.ToImmutable();
        }

        /// <summary>
        /// Returns allocation information for the given method.
        /// </summary>
        public Allocas this[Method method] => _allocations[method];

        /// <summary>
        /// Returns the shared memory mode.
        /// </summary>
        public CompiledKernelSharedMemoryMode SharedMemoryMode { get; }

        /// <summary>
        /// Returns the maximum size of shared memory required in bytes.
        /// </summary>
        public int SharedMemorySize { get; }

        /// <summary>
        /// Returns the maximum size of local memory required in bytes.
        /// </summary>
        public int LocalMemorySize { get; }

        /// <summary>
        /// Returns all dynamic allocations.
        /// </summary>
        public ImmutableArray<AllocaInformation> DynamicAllocations { get; }
    }

    /// <summary>
    /// Returns the associated backend type.
    /// </summary>
    public BackendType BackendType { get; } = backendType;

    /// <summary>
    /// Returns the associated accelerator type.
    /// </summary>
    public AcceleratorType AcceleratorType { get; } = acceleratorType;

    /// <summary>
    /// Returns the current warp size (if available).
    /// </summary>
    public virtual int? CurrentWarpSize { get; }

    /// <summary>
    /// Returns the supported capabilities.
    /// </summary>
    public CapabilityContext Capabilities { get; } = capabilities;

    /// <summary>
    /// Compiles a given method into a compiled kernel.
    /// </summary>
    /// <param name="frontend">
    /// Frontend instance to be used for intrinsic code generation.
    /// </param>
    /// <param name="entryPoint">The entry point to use.</param>
    /// <param name="context">The current context.</param>
    /// <returns>The compiled kernel that represents the compilation result.</returns>
    public CompiledKernelData Compile(
        ILFrontend frontend,
        EntryPoint entryPoint,
        IRContext context)
    {
        // Import the all kernel functions into a new kernel context
        var targetMethod = context.GetMethod(entryPoint.Method);

        try
        {
            using var kernelContext = targetMethod.ExtractToContext(out var kernelMethod);
            kernelMethod.AddFlags(MethodFlags.EntryPoint);

            // Create transformation pipeline
            var pipelineBuilder = Transformer.CreateBuilder();
            var transformationPipeline = CreateTransformer(
                frontend,
                kernelContext,
                pipelineBuilder);

            // Apply backend transformations
            kernelContext.Transform(transformationPipeline);

            return Compile(entryPoint, kernelContext, kernelMethod);
        }
        catch (InternalCompilerException)
        {
            // If we already have an internal compiler exception, re-throw it.
            throw;
        }
        catch (Exception e)
        {
            // Wrap generic exceptions.
            throw new InternalCompilerException(
                ErrorMessages.InternalCompilerError,
                e);
        }
    }

    /// <summary>
    /// Creates a new backend transformer to be used with the given context.
    /// </summary>
    /// <param name="frontend">
    /// Frontend instance to be used for intrinsic code generation.
    /// </param>
    /// <param name="context">The kernel context.</param>
    /// <param name="builder">The transformation pipeline builder.</param>
    /// <returns>The final transformer to use.</returns>
    protected abstract Transformer CreateTransformer(
        ILFrontend frontend,
        IRContext context,
        Transformer.Builder builder);

    /// <summary>
    /// Compiles a given compile unit with the specified entry point using
    /// the given kernel specialization and the placement information.
    /// </summary>
    /// <param name="entryPoint">The desired entry point.</param>
    /// <param name="kernelContext">
    /// The current kernel context containing all required functions.
    /// </param>
    /// <param name="kernelMethod">The kernel method entry point.</param>
    /// <returns>
    /// The compiled kernel that represents the compilation result.
    /// </returns>
    protected abstract CompiledKernelData Compile(
        EntryPoint entryPoint,
        IRContext kernelContext,
        Method kernelMethod);
}

/// <summary>
/// Represents an abstract code generator that works on a given data type.
/// </summary>
/// <typeparam name="TKernelBuilder">
/// The data type on which this code generator can work.
/// </typeparam>
interface IBackendCodeGenerator<TKernelBuilder>
{
    /// <summary>
    /// Generates all constant definitions (if any).
    /// </summary>
    /// <param name="builder">The current builder.</param>
    void GenerateConstants(TKernelBuilder builder);

    /// <summary>
    /// Generates a header definition (if any).
    /// </summary>
    /// <param name="builder">The current builder.</param>
    void GenerateHeader(TKernelBuilder builder);

    /// <summary>
    /// Generates the actual function code.
    /// </summary>
    void GenerateCode();

    /// <summary>
    /// Merges all changes inside the current code generator into the given builder.
    /// </summary>
    /// <param name="builder">The builder to merge with.</param>
    void Merge(TKernelBuilder builder);
}

/// <summary>
/// A backend using custom code generators and kernel builders.
/// </summary>
/// <typeparam name="T">Backend-specific custom data.</typeparam>
/// <typeparam name="TCodeGenerator">The code generator type to use.</typeparam>
/// <typeparam name="TKernelBuilder">The custom kernel builder type.</typeparam>
/// <param name="backendType">The backend type.</param>
/// <param name="acceleratorType">The accelerator type.</param>
/// <param name="capabilities">The supported capabilities.</param>
abstract class Backend<T, TCodeGenerator, TKernelBuilder>(
    BackendType backendType,
    AcceleratorType acceleratorType,
    CapabilityContext capabilities) :
    Backend(backendType, acceleratorType, capabilities)
    where TCodeGenerator : class, IBackendCodeGenerator<TKernelBuilder>
{
    /// <summary>
    /// Compiles the given context and entry point information into binary form.
    /// </summary>
    protected sealed override CompiledKernelData Compile(
        EntryPoint entryPoint,
        IRContext kernelContext,
        Method kernelMethod)
    {
        // Compute shared memory allocations
        var allocations = new Allocations(kernelContext, kernelMethod);

        // Initialize the main builder and generator
        var mainBuilder = CreateKernelBuilder(
            entryPoint,
            kernelContext,
            allocations,
            out var data);

        // Create kernel generator
        var generators = new List<TCodeGenerator>(kernelContext.Methods.Count)
        {
            CreateKernelCodeGenerator(kernelMethod, data)
        };

        // Create all remaining builders and code generators
        foreach (var method in kernelContext.GetMethodCollection(m => m != kernelMethod))
        {
            var gen = CreateFunctionCodeGenerator(method, data);
            generators.Add(gen);
        }

        // Generate code
        foreach (var generator in generators)
            generator.GenerateCode();

        // Generate all constants
        foreach (var generator in generators)
            generator.GenerateConstants(mainBuilder);

        // Declare all methods
        foreach (var generator in generators)
            generator.GenerateHeader(mainBuilder);

        // Merge all code generators in reverse order
        for (int i = generators.Count - 1; i >= 0; --i)
            generators[i].Merge(mainBuilder);

        // Serialize data
        var kernelData = SerializeBuilder(mainBuilder, data, out var customAttributes);

        // Create kernel data
        return entryPoint.CreateCompiledKernelData(
            allocations.SharedMemoryMode,
            allocations.SharedMemorySize,
            allocations.LocalMemorySize,
            kernelData,
            customAttributes);
    }

    /// <summary>
    /// Creates the main kernel builder and initializes
    /// all required information.
    /// </summary>
    /// <param name="entryPoint">The current entry point.</param>
    /// <param name="context">The backend context.</param>
    /// <param name="allocations">Allocation information for the kernel program.</param>
    /// <param name="data">Custom backend data.</param>
    /// <returns>The resulting kernel builder.</returns>
    protected abstract TKernelBuilder CreateKernelBuilder(
        EntryPoint entryPoint,
        IRContext context,
        Allocations allocations,
        out T data);

    /// <summary>
    /// Creates a new function-code generator.
    /// </summary>
    /// <param name="method">The current method.</param>
    /// <param name="data">Custom backend data.</param>
    /// <returns>The created function-code generator.</returns>
    protected abstract TCodeGenerator CreateFunctionCodeGenerator(Method method, T data);

    /// <summary>
    /// Creates a new kernel-code generator.
    /// </summary>
    /// <param name="method">The current method.</param>
    /// <param name="data">Custom backend data.</param>
    /// <returns>The created kernel-code generator.</returns>
    protected abstract TCodeGenerator CreateKernelCodeGenerator(Method method, T data);

    /// <summary>
    /// Serializes the given builder into binary form.
    /// </summary>
    /// <param name="builder">The builder to serialize.</param>
    /// <param name="data">Custom backend data.</param>
    /// <param name="customAttributes">Custom attributes to store (optional).</param>
    /// <returns>The serialized representation of the builder.</returns>
    protected abstract ReadOnlyMemory<byte> SerializeBuilder(
        TKernelBuilder builder,
        T data,
        out ReadOnlyMemory<byte> customAttributes);
}
